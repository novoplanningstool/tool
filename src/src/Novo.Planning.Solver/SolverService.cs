using Google.OrTools.LinearSolver;
using Novo.Planning.Domain.Models;
using OrToolsSolver = Google.OrTools.LinearSolver.Solver;

namespace Novo.Planning.Solver;

/// <summary>
/// Main solver service: builds model, solves with relaxation fallback, extracts solution.
/// Port of build_and_solve() from solver.py.
/// </summary>
public class SolverService : ISolverService
{
    private static readonly (bool IncludeLanguage, string SkillLevelMode)[] RelaxationConfigs =
    [
        (true, "full"),      // attempt 0: full model
        (false, "full"),     // attempt 1: remove language
        (true, "partial"),   // attempt 2: remove level 3 max
        (true, "none"),      // attempt 3: remove all skill levels
        (false, "none"),     // attempt 4: remove skill levels + language
    ];

    public SolverResult Solve(List<Person> presentWorkers, List<TaskDefinition> activeTasks, OptimizationStrategy strategy)
    {
        var parameters = SolverParameterBuilder.Build(presentWorkers, activeTasks);
        return SolveInternal(parameters, strategy);
    }

    internal static SolverResult SolveWithParameters(SolverParameters parameters, OptimizationStrategy strategy)
    {
        return new SolverService().SolveInternal(parameters, strategy);
    }

    private SolverResult SolveInternal(SolverParameters parameters, OptimizationStrategy strategy)
    {
        var warnings = new List<string>();

        for (int attempt = 0; attempt < RelaxationConfigs.Length; attempt++)
        {
            var (includeLanguage, skillLevelMode) = RelaxationConfigs[attempt];

            var (solver, assignmentVars) = ModelBuilder.Build(
                parameters, strategy, includeLanguage, skillLevelMode);

            solver.SetTimeLimit(300_000); // 300 seconds

            var status = solver.Solve();

            if (status == OrToolsSolver.ResultStatus.OPTIMAL ||
                status == OrToolsSolver.ResultStatus.FEASIBLE)
            {
                var assignments = ExtractAssignments(assignmentVars, parameters);

                if (attempt == 0)
                {
                    return new SolverResult
                    {
                        Status = SolverStatus.Optimal,
                        Assignments = assignments,
                    };
                }

                // Relaxed — add appropriate warnings with task names
                warnings.AddRange(GetRelaxationWarnings(attempt, assignments, parameters));

                return new SolverResult
                {
                    Status = SolverStatus.Relaxed,
                    Assignments = assignments,
                    RelaxationAttempt = attempt,
                    Warnings = warnings,
                };
            }

            if (status != OrToolsSolver.ResultStatus.INFEASIBLE)
            {
                return new SolverResult
                {
                    Status = SolverStatus.Other,
                    Warnings = ["Model status is niet optimaal, maar ook niet infeasible"],
                };
            }

            // INFEASIBLE — continue to next relaxation
            solver.Dispose();
        }

        // All attempts exhausted — diagnose infeasibility
        return DiagnoseInfeasibility(parameters);
    }

    private static List<PlanningAssignment> ExtractAssignments(Variable[] assignmentVars, SolverParameters parameters)
    {
        var assignments = new List<PlanningAssignment>();
        for (int i = 0; i < assignmentVars.Length; i++)
        {
            if (assignmentVars[i].SolutionValue() > 0.5)
            {
                var (level, worker, task) = parameters.SkillIndexTuples[i];
                var actualSkill = parameters.SkillMatrix[worker, task];
                assignments.Add(new PlanningAssignment
                {
                    TaskName = parameters.TaskNames[task],
                    WorkerName = parameters.WorkerNames[worker],
                    SkillLevel = (SkillLevel)actualSkill,
                });
            }
        }
        return assignments;
    }

    private static List<string> GetRelaxationWarnings(int attempt, List<PlanningAssignment> assignments, SolverParameters parameters)
    {
        var taskNames = FindNonCompliantTasks(attempt, assignments, parameters);
        var taskList = taskNames.Count > 0
            ? "\n\nBij taken: " + string.Join(", ", taskNames)
            : "";

        return attempt switch
        {
            1 => [$"LET OP! De planning voldoet niet aan de volgende eis:\n\n* Werknemers spreken niet overal dezelfde taal, waar nodig{taskList}"],
            2 => [$"LET OP! De planning voldoet niet aan de volgende eis:\n\n* Er zijn voldoende Experts ingedeeld, maar er wordt niet voldaan aan de minimum eisen van Ervaren en Beginner{taskList}"],
            3 => [$"LET OP! De planning voldoet niet aan de volgende eis:\n\n* Er wordt niet aan de minimum eisen van de competentieniveaus (Expert, Ervaren, Beginner) voldaan{taskList}"],
            4 => [$"LET OP! De planning voldoet niet aan de volgende eisen:\n\n* Er wordt niet aan de minimum eisen van de competentieniveaus (Expert, Ervaren, Beginner) voldaan{taskList}\n\n* Werknemers spreken niet overal dezelfde taal, waar nodig"],
            _ => []
        };
    }

    private static List<string> FindNonCompliantTasks(int attempt, List<PlanningAssignment> assignments, SolverParameters parameters)
    {
        var taskNames = new List<string>();

        var assignmentsByTask = assignments
            .GroupBy(a => a.TaskName)
            .ToDictionary(g => g.Key, g => g.ToList());

        for (int t = 0; t < parameters.TaskNames.Length; t++)
        {
            var name = parameters.TaskNames[t];
            var taskAssignments = assignmentsByTask.GetValueOrDefault(name, []);

            var level1Count = taskAssignments.Count(a => a.SkillLevel == SkillLevel.Expert);
            var level3Count = taskAssignments.Count(a => a.SkillLevel == SkillLevel.Beginner);

            var isNonCompliant = false;

            // Attempts 2-4: check if level 3 workers exceed the max (which is MinWorkersLevel3)
            if (attempt is 2 or 4)
            {
                var maxL3 = parameters.MaxLevel3PerTask[t];
                if (level3Count > maxL3)
                    isNonCompliant = true;
            }

            // Attempts 3-4: check if level 1 minimum not met
            if (attempt is 3 or 4)
            {
                var minL1 = parameters.MinLevel1PerTask[t];
                if (minL1 > 0 && level1Count < minL1)
                    isNonCompliant = true;
            }

            // Attempt 1: language issues checked separately below
            if (isNonCompliant)
                taskNames.Add(name);
        }

        // For language relaxation (attempts 1, 4), find collab tasks with incompatible workers
        if (attempt is 1 or 4)
        {
            for (int t = 0; t < parameters.TaskNames.Length; t++)
            {
                if (!parameters.TaskRequiresCollaboration[t]) continue;
                var name = parameters.TaskNames[t];
                if (taskNames.Contains(name)) continue;

                var taskAssignments = assignmentsByTask.GetValueOrDefault(name, []);
                if (taskAssignments.Count < 2) continue;

                // Check if all workers share a language
                var workerNames = taskAssignments.Select(a => a.WorkerName).ToList();
                var workerIndices = workerNames
                    .Select(wn => Array.IndexOf(parameters.WorkerNames, wn))
                    .Where(i => i >= 0)
                    .ToList();

                var allDutch = workerIndices.All(i => parameters.WorkerSpeaksDutch[i]);
                var allPolish = workerIndices.All(i => parameters.WorkerSpeaksPolish[i]);

                if (!allDutch && !allPolish)
                    taskNames.Add(name);
            }
        }

        return taskNames;
    }

    public SolverResult ValidateAssignments(List<Person> presentWorkers, List<TaskDefinition> activeTasks, List<PlanningAssignment> assignments)
    {
        var parameters = SolverParameterBuilder.Build(presentWorkers, activeTasks);

        // Check each constraint type independently using FindNonCompliantTasks:
        // attempt 1 checks language only, attempt 2 checks L3 max only, attempt 3 checks L1 min only
        var hasLanguageViolation = FindNonCompliantTasks(1, assignments, parameters).Count > 0;
        var hasL3MaxViolation = FindNonCompliantTasks(2, assignments, parameters).Count > 0;
        var hasL1MinViolation = FindNonCompliantTasks(3, assignments, parameters).Count > 0;

        if (!hasLanguageViolation && !hasL3MaxViolation && !hasL1MinViolation)
        {
            return new SolverResult
            {
                Status = SolverStatus.Optimal,
                Assignments = assignments,
            };
        }

        // Determine which relaxation attempt the assignments would need:
        // Attempt 1: removes language constraint
        // Attempt 2: removes L3 max constraint
        // Attempt 3: removes all skill level constraints
        // Attempt 4: removes skill levels + language
        int attempt;
        if (!hasL3MaxViolation && !hasL1MinViolation)
            attempt = 1; // only language violated
        else if (!hasLanguageViolation && !hasL1MinViolation)
            attempt = 2; // only L3 max violated
        else if (!hasLanguageViolation)
            attempt = 3; // skill levels violated, language ok
        else
            attempt = 4; // language + skill levels violated

        var warnings = GetRelaxationWarnings(attempt, assignments, parameters);

        return new SolverResult
        {
            Status = SolverStatus.Relaxed,
            Assignments = assignments,
            RelaxationAttempt = attempt,
            Warnings = warnings,
        };
    }

    private static SolverResult DiagnoseInfeasibility(SolverParameters parameters)
    {
        var warnings = new List<string>();
        var level4OnlyWorkers = new List<string>();
        var level4OnlyTasks = new List<string>();

        // Check headcount: total required vs available workers
        var totalHeadcount = parameters.TaskHeadcounts.Sum();
        var totalWorkers = parameters.WorkerIndices.Length;
        if (totalWorkers < totalHeadcount)
        {
            warnings.Add($"Er zijn niet genoeg medewerkers ({totalWorkers}) voor het totaal aantal benodigde plekken ({totalHeadcount}). Voeg meer medewerkers toe, verwijder taken, of verlaag het aantal medewerkers per taak.");
        }

        // Find workers with only level-4 skills
        foreach (var worker in parameters.WorkerIndices)
        {
            var hasEligible = false;
            for (int i = 0; i < parameters.SkillIndexTuples.Length; i++)
            {
                if (parameters.SkillIndexTuples[i].Worker == worker && parameters.SkillEligible[i] == 1)
                {
                    hasEligible = true;
                    break;
                }
            }
            if (!hasEligible)
            {
                level4OnlyWorkers.Add(parameters.WorkerNames[worker]);
            }
        }

        // Find tasks with no eligible workers
        foreach (var task in parameters.TaskIndices)
        {
            var hasEligible = false;
            for (int i = 0; i < parameters.SkillIndexTuples.Length; i++)
            {
                if (parameters.SkillIndexTuples[i].Task == task && parameters.SkillEligible[i] == 1)
                {
                    hasEligible = true;
                    break;
                }
            }
            if (!hasEligible)
            {
                level4OnlyTasks.Add(parameters.TaskNames[task]);
            }
        }

        // Generate Dutch warning messages (matching Python exactly)
        if (level4OnlyWorkers.Count == 1)
        {
            warnings.Add($"{level4OnlyWorkers[0]} heeft op de huidige taken alleen het niveau 'Kan niet', diegene kan dus nergens ingepland worden en daardoor loopt de planning fout. Selecteer een andere taak, een andere medewerker of pas het competentieniveau van deze persoon aan.");
        }
        else if (level4OnlyWorkers.Count > 1)
        {
            warnings.Add(string.Join(" en ", level4OnlyWorkers) + " hebben op de huidige taken alleen het niveau 'Kan niet'. Zij kunnen dus niet worden ingepland en daardoor loopt de planning fout. Selecteer andere taken, andere medewerkers of pas de competentieniveaus van deze personen aan.");
        }

        if (level4OnlyTasks.Count == 1)
        {
            warnings.Add($"Voor de taak {level4OnlyTasks[0]} is niemand aanwezig die niet het niveau 'Kan niet' heeft. Daardoor kan er niemand worden ingepland. Zorg ervoor dat er een werknemer met een ander niveau aanwezig is, of dat de taak niet wordt uitgevoerd.");
        }
        else if (level4OnlyTasks.Count > 1)
        {
            warnings.Add("Voor de taken " + string.Join(" en ", level4OnlyTasks) + " zijn er geen werknemers aanwezig die niet het niveau 'Kan niet' hebben. Daardoor kan er niemand worden ingepland. Zorg ervoor dat er werknemers met een ander niveau aanwezig zijn, of dat de taken niet worden uitgevoerd.");
        }

        // Fallback if no specific cause was identified
        if (warnings.Count == 0)
        {
            warnings.Add("De combinatie van geselecteerde medewerkers en taken kan niet worden opgelost. Probeer meer medewerkers te selecteren, taken te verwijderen, of het aantal medewerkers per taak te verlagen.");
        }

        return new SolverResult
        {
            Status = SolverStatus.Infeasible,
            RelaxationAttempt = RelaxationConfigs.Length,
            Warnings = warnings,
            Level4OnlyWorkers = level4OnlyWorkers,
            Level4OnlyTasks = level4OnlyTasks,
        };
    }
}
