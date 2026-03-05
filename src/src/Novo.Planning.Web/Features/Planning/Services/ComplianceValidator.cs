using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public class ComplianceValidator : IComplianceValidator
{
    public List<ComplianceViolation> Validate(
        PlanningModel planning,
        List<Person> persons,
        List<TaskDefinition> tasks)
    {
        var violations = new List<ComplianceViolation>();

        var taskLookup = tasks.ToDictionary(t => t.Name, t => t);
        var personLookup = persons.ToDictionary(p => p.Name, p => p);

        // Group assignments by task
        var assignmentsByTask = planning.Assignments
            .GroupBy(a => a.TaskName)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group assignments by worker
        var assignmentsByWorker = planning.Assignments
            .GroupBy(a => a.WorkerName)
            .ToDictionary(g => g.Key, g => g.ToList());

        CheckHeadcount(violations, taskLookup, assignmentsByTask);
        CheckSkillMinimums(violations, taskLookup, assignmentsByTask);
        CheckLanguageCollaboration(violations, taskLookup, assignmentsByTask, personLookup);
        CheckWorkerOverallocation(violations, assignmentsByWorker);
        CheckSkillEligibility(violations, planning.Assignments, personLookup, taskLookup);
        CheckUnassignedWorkers(violations, persons, planning, assignmentsByWorker);

        return violations;
    }

    /// <summary>
    /// Checks that each task has the required number of workers assigned.
    /// </summary>
    private static void CheckHeadcount(
        List<ComplianceViolation> violations,
        Dictionary<string, TaskDefinition> taskLookup,
        Dictionary<string, List<PlanningAssignment>> assignmentsByTask)
    {
        foreach (var (taskName, task) in taskLookup)
        {
            var assignedCount = assignmentsByTask.TryGetValue(taskName, out var assignments)
                ? assignments.Count
                : 0;

            if (assignedCount < task.HeadcountRequired)
            {
                violations.Add(new ComplianceViolation
                {
                    Severity = ViolationSeverity.Error,
                    Message = $"Taak '{taskName}' heeft {assignedCount} van de {task.HeadcountRequired} vereiste medewerkers.",
                    TaskName = taskName
                });
            }
        }
    }

    /// <summary>
    /// Checks that each task meets its minimum skill level requirements
    /// (MinWorkersLevel1, MinWorkersLevel2, MinWorkersLevel3).
    /// </summary>
    private static void CheckSkillMinimums(
        List<ComplianceViolation> violations,
        Dictionary<string, TaskDefinition> taskLookup,
        Dictionary<string, List<PlanningAssignment>> assignmentsByTask)
    {
        foreach (var (taskName, task) in taskLookup)
        {
            if (!assignmentsByTask.TryGetValue(taskName, out var assignments))
                continue;

            // Workers at or better than level 1 (Expert)
            var level1Count = assignments.Count(a => a.SkillLevel <= SkillLevel.Expert);
            if (level1Count < task.MinWorkersLevel1)
            {
                violations.Add(new ComplianceViolation
                {
                    Severity = ViolationSeverity.Warning,
                    Message = $"Taak '{taskName}' heeft {level1Count} van de {task.MinWorkersLevel1} vereiste niveau 1 (Expert) medewerkers.",
                    TaskName = taskName
                });
            }

            // Workers at or better than level 2 (Experienced)
            var level2Count = assignments.Count(a => a.SkillLevel <= SkillLevel.Experienced);
            if (level2Count < task.MinWorkersLevel2)
            {
                violations.Add(new ComplianceViolation
                {
                    Severity = ViolationSeverity.Warning,
                    Message = $"Taak '{taskName}' heeft {level2Count} van de {task.MinWorkersLevel2} vereiste niveau 2 (Experienced) medewerkers.",
                    TaskName = taskName
                });
            }

            // Workers at or better than level 3 (Beginner)
            var level3Count = assignments.Count(a => a.SkillLevel <= SkillLevel.Beginner);
            if (level3Count < task.MinWorkersLevel3)
            {
                violations.Add(new ComplianceViolation
                {
                    Severity = ViolationSeverity.Warning,
                    Message = $"Taak '{taskName}' heeft {level3Count} van de {task.MinWorkersLevel3} vereiste niveau 3 (Beginner) medewerkers.",
                    TaskName = taskName
                });
            }
        }
    }

    /// <summary>
    /// Checks that tasks requiring language collaboration have workers who share a common language.
    /// </summary>
    private static void CheckLanguageCollaboration(
        List<ComplianceViolation> violations,
        Dictionary<string, TaskDefinition> taskLookup,
        Dictionary<string, List<PlanningAssignment>> assignmentsByTask,
        Dictionary<string, Person> personLookup)
    {
        foreach (var (taskName, task) in taskLookup)
        {
            if (!task.RequiresLanguageCollaboration)
                continue;

            if (!assignmentsByTask.TryGetValue(taskName, out var assignments) || assignments.Count < 2)
                continue;

            var workers = assignments
                .Select(a => personLookup.GetValueOrDefault(a.WorkerName))
                .Where(p => p != null)
                .Cast<Person>()
                .ToList();

            if (workers.Count < 2)
                continue;

            // Check all pairs share at least one common language
            for (int i = 0; i < workers.Count - 1; i++)
            {
                for (int j = i + 1; j < workers.Count; j++)
                {
                    bool dutchShared = workers[i].SpeaksDutch && workers[j].SpeaksDutch;
                    bool polishShared = workers[i].SpeaksPolish && workers[j].SpeaksPolish;

                    if (!dutchShared && !polishShared)
                    {
                        violations.Add(new ComplianceViolation
                        {
                            Severity = ViolationSeverity.Warning,
                            Message = $"Taak '{taskName}': {workers[i].Name} en {workers[j].Name} delen geen gemeenschappelijke taal.",
                            TaskName = taskName
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks that no worker is assigned to more than one task.
    /// </summary>
    private static void CheckWorkerOverallocation(
        List<ComplianceViolation> violations,
        Dictionary<string, List<PlanningAssignment>> assignmentsByWorker)
    {
        foreach (var (workerName, assignments) in assignmentsByWorker)
        {
            if (assignments.Count > 1)
            {
                var taskNames = string.Join(", ", assignments.Select(a => $"'{a.TaskName}'"));
                violations.Add(new ComplianceViolation
                {
                    Severity = ViolationSeverity.Error,
                    Message = $"Medewerker '{workerName}' is toegewezen aan meerdere taken: {taskNames}.",
                    WorkerName = workerName
                });
            }
        }
    }

    /// <summary>
    /// Checks that all present workers (not absent, not temp template) are assigned to a task.
    /// </summary>
    private static void CheckUnassignedWorkers(
        List<ComplianceViolation> violations,
        List<Person> persons,
        PlanningModel planning,
        Dictionary<string, List<PlanningAssignment>> assignmentsByWorker)
    {
        var absentNames = planning.AbsentWorkers.ToHashSet();

        var unassigned = persons
            .Where(p => p.Id != "temp-worker-template"
                        && !absentNames.Contains(p.Name)
                        && !assignmentsByWorker.ContainsKey(p.Name))
            .Select(p => p.Name)
            .ToList();

        if (unassigned.Count > 0)
        {
            var names = string.Join(", ", unassigned);
            var label = unassigned.Count == 1 ? "medewerker is" : "medewerkers zijn";
            violations.Add(new ComplianceViolation
            {
                Severity = ViolationSeverity.Warning,
                Message = $"{unassigned.Count} aanwezige {label} niet ingepland: {names}."
            });
        }
    }

    /// <summary>
    /// Checks that no worker with skill level 4 (Cannot) is assigned to a task.
    /// </summary>
    private static void CheckSkillEligibility(
        List<ComplianceViolation> violations,
        List<PlanningAssignment> assignments,
        Dictionary<string, Person> personLookup,
        Dictionary<string, TaskDefinition> taskLookup)
    {
        foreach (var assignment in assignments)
        {
            if (assignment.SkillLevel == SkillLevel.Cannot)
            {
                violations.Add(new ComplianceViolation
                {
                    Severity = ViolationSeverity.Error,
                    Message = $"Medewerker '{assignment.WorkerName}' heeft niveau 4 (Cannot) voor taak '{assignment.TaskName}' en mag hier niet worden ingepland.",
                    TaskName = assignment.TaskName,
                    WorkerName = assignment.WorkerName
                });
                continue;
            }

            // Also check against the person's actual skills if available
            if (personLookup.TryGetValue(assignment.WorkerName, out var person) &&
                taskLookup.ContainsKey(assignment.TaskName))
            {
                if (person.Skills.TryGetValue(assignment.TaskName, out var actualLevel) &&
                    actualLevel == SkillLevel.Cannot)
                {
                    violations.Add(new ComplianceViolation
                    {
                        Severity = ViolationSeverity.Error,
                        Message = $"Medewerker '{assignment.WorkerName}' heeft volgens het competentieprofiel niveau 4 (Cannot) voor taak '{assignment.TaskName}'.",
                        TaskName = assignment.TaskName,
                        WorkerName = assignment.WorkerName
                    });
                }
            }
        }
    }
}
