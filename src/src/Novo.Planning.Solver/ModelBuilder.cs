using Google.OrTools.LinearSolver;
using Novo.Planning.Domain.Models;
using OrToolsSolver = Google.OrTools.LinearSolver.Solver;

namespace Novo.Planning.Solver;

/// <summary>
/// Builds the MIP model with configurable constraint groups.
/// Port of _build_model() from solver.py.
/// </summary>
public static class ModelBuilder
{
    public static (OrToolsSolver solver, Variable[] assignmentVars) Build(
        SolverParameters parameters,
        OptimizationStrategy strategy,
        bool includeLanguage = true,
        string skillLevelMode = "full")
    {
        var solver = OrToolsSolver.CreateSolver("CBC");
        if (solver == null) throw new InvalidOperationException("Could not create CBC solver");

        var levels = parameters.Levels;
        var taskIndices = parameters.TaskIndices;
        var workerIndices = parameters.WorkerIndices;
        var skillIndexTuples = parameters.SkillIndexTuples;
        var skillEligible = parameters.SkillEligible;
        var teamsizeIndexTuples = parameters.TeamsizeIndexTuples;
        var minWorkersPerLevel = parameters.MinWorkersPerLevel;

        var totalVars = skillIndexTuples.Length;

        // --- Decision Variables ---
        var assignmentVars = new Variable[totalVars];
        for (int i = 0; i < totalVars; i++)
        {
            var (l, w, t) = skillIndexTuples[i];
            assignmentVars[i] = solver.MakeBoolVar($"x({l},{w},{t})");
        }

        // Collaboration variables for Samenwerken tasks
        var collabTasks = taskIndices.Where(t => parameters.TaskRequiresCollaboration[t]).ToArray();
        var collabIndexTuples = new List<(int W1, int W2, int T)>();
        foreach (var w1 in workerIndices)
            foreach (var w2 in workerIndices)
                foreach (var t in collabTasks)
                    collabIndexTuples.Add((w1, w2, t));

        var collabVars = new Variable[collabIndexTuples.Count];
        for (int i = 0; i < collabIndexTuples.Count; i++)
        {
            var (w1, w2, t) = collabIndexTuples[i];
            collabVars[i] = solver.MakeBoolVar($"t({w1},{w2},{t})");
        }

        // --- Objective Functions ---
        BuildObjective(solver, strategy, assignmentVars, skillIndexTuples,
            teamsizeIndexTuples, minWorkersPerLevel, parameters);

        // --- Constraints ---

        // CONSTRAINT 1: each worker gets exactly 1 task
        foreach (var worker in workerIndices)
        {
            var ct = solver.MakeConstraint(1, 1, $"worker_{worker}_one_task");
            for (int i = 0; i < totalVars; i++)
            {
                if (skillIndexTuples[i].Worker == worker)
                    ct.SetCoefficient(assignmentVars[i], 1);
            }
        }

        // CONSTRAINT 2: skill eligibility
        for (int i = 0; i < totalVars; i++)
        {
            if (skillEligible[i] == 0)
            {
                var ct = solver.MakeConstraint(0, 0, $"skill_elig_{i}");
                ct.SetCoefficient(assignmentVars[i], 1);
            }
        }

        // CONSTRAINT 3: each task gets exactly the required headcount
        foreach (var task in taskIndices)
        {
            var headcount = parameters.TaskHeadcounts[task];
            var ct = solver.MakeConstraint(headcount, headcount, $"task_{task}_headcount");
            for (int i = 0; i < totalVars; i++)
            {
                if (skillIndexTuples[i].Task == task)
                    ct.SetCoefficient(assignmentVars[i], 1);
            }
        }

        // CONSTRAINT 4: minimum skill level requirements
        if (skillLevelMode is "full" or "partial")
        {
            foreach (var task in taskIndices)
            {
                var minL1 = parameters.MinLevel1PerTask[task];
                if (minL1 > 0)
                {
                    var ct = solver.MakeConstraint(minL1, double.PositiveInfinity, $"min_l1_task_{task}");
                    for (int i = 0; i < totalVars; i++)
                    {
                        if (skillIndexTuples[i].Level == 1 && skillIndexTuples[i].Task == task)
                            ct.SetCoefficient(assignmentVars[i], 1);
                    }
                }
            }

            if (skillLevelMode == "full")
            {
                foreach (var task in taskIndices)
                {
                    var maxL3 = parameters.MaxLevel3PerTask[task];
                    var ct = solver.MakeConstraint(double.NegativeInfinity, maxL3, $"max_l3_task_{task}");
                    for (int i = 0; i < totalVars; i++)
                    {
                        if (skillIndexTuples[i].Level == 3 && skillIndexTuples[i].Task == task)
                            ct.SetCoefficient(assignmentVars[i], 1);
                    }
                }
            }
        }

        // CONSTRAINT 5: language compatibility
        if (includeLanguage && collabTasks.Length > 0)
        {
            BuildLanguageConstraints(solver, assignmentVars, collabVars,
                skillIndexTuples, collabIndexTuples, parameters);
        }

        return (solver, assignmentVars);
    }

    private static void BuildObjective(
        OrToolsSolver solver,
        OptimizationStrategy strategy,
        Variable[] assignmentVars,
        (int Level, int Worker, int Task)[] skillIndexTuples,
        (int Level, int Task)[] teamsizeIndexTuples,
        int[] minWorkersPerLevel,
        SolverParameters parameters)
    {
        var objective = solver.Objective();

        switch (strategy)
        {
            case OptimizationStrategy.MaximizeExpertise:
            {
                // Maximize: sum(x(1,w,t) + 0.5*x(2,w,t)) for all w,t
                objective.SetMaximization();
                for (int i = 0; i < skillIndexTuples.Length; i++)
                {
                    var level = skillIndexTuples[i].Level;
                    if (level == 1) objective.SetCoefficient(assignmentVars[i], 1.0);
                    else if (level == 2) objective.SetCoefficient(assignmentVars[i], 0.5);
                }
                break;
            }
            case OptimizationStrategy.LearningFocused:
            {
                // Minimize: sum(u(l,t)) for all l,t
                // With |sum_w x(l,w,t) - min_workers(l,t)| <= u(l,t)
                objective.SetMinimization();
                var deviationVars = CreateDeviationVarsAndConstraints(
                    solver, objective, assignmentVars, skillIndexTuples,
                    teamsizeIndexTuples, minWorkersPerLevel, parameters);
                break;
            }
            case OptimizationStrategy.Hybrid:
            {
                // Minimize: sum(u(l,t)/2) + sum(x(3,w,t)) + sum(x(2,w,t)/2)
                objective.SetMinimization();
                var deviationVars = CreateDeviationVarsAndConstraints(
                    solver, objective, assignmentVars, skillIndexTuples,
                    teamsizeIndexTuples, minWorkersPerLevel, parameters,
                    deviationWeight: 0.5);

                // Add level-3 and level-2 terms to objective
                for (int i = 0; i < skillIndexTuples.Length; i++)
                {
                    var level = skillIndexTuples[i].Level;
                    if (level == 3) objective.SetCoefficient(assignmentVars[i], 1.0);
                    else if (level == 2) objective.SetCoefficient(assignmentVars[i], 0.5);
                }
                break;
            }
        }
    }

    private static Variable[] CreateDeviationVarsAndConstraints(
        OrToolsSolver solver,
        Objective objective,
        Variable[] assignmentVars,
        (int Level, int Worker, int Task)[] skillIndexTuples,
        (int Level, int Task)[] teamsizeIndexTuples,
        int[] minWorkersPerLevel,
        SolverParameters parameters,
        double deviationWeight = 1.0)
    {
        var deviationVars = new Variable[teamsizeIndexTuples.Length];
        for (int i = 0; i < teamsizeIndexTuples.Length; i++)
        {
            var (l, t) = teamsizeIndexTuples[i];
            deviationVars[i] = solver.MakeIntVar(0, double.PositiveInfinity, $"u({l},{t})");
            objective.SetCoefficient(deviationVars[i], deviationWeight);
        }

        // Absolute value constraints: |sum_w x(l,w,t) - min_workers(l,t)| <= u(l,t)
        // This is: sum_w x(l,w,t) - min_workers(l,t) <= u(l,t)
        //    AND: -(sum_w x(l,w,t) - min_workers(l,t)) <= u(l,t)
        foreach (var level in parameters.Levels)
        {
            foreach (var task in parameters.TaskIndices)
            {
                int tsIdx = -1;
                for (int i = 0; i < teamsizeIndexTuples.Length; i++)
                {
                    if (teamsizeIndexTuples[i].Level == level && teamsizeIndexTuples[i].Task == task)
                    {
                        tsIdx = i;
                        break;
                    }
                }
                var minWorkers = minWorkersPerLevel[tsIdx];

                // sum_w x(l,w,t) - u(l,t) <= min_workers
                var ct1 = solver.MakeConstraint(double.NegativeInfinity, minWorkers, $"dev_pos_{level}_{task}");
                ct1.SetCoefficient(deviationVars[tsIdx], -1);
                for (int i = 0; i < skillIndexTuples.Length; i++)
                {
                    if (skillIndexTuples[i].Level == level && skillIndexTuples[i].Task == task)
                        ct1.SetCoefficient(assignmentVars[i], 1);
                }

                // -sum_w x(l,w,t) - u(l,t) <= -min_workers
                var ct2 = solver.MakeConstraint(double.NegativeInfinity, -minWorkers, $"dev_neg_{level}_{task}");
                ct2.SetCoefficient(deviationVars[tsIdx], -1);
                for (int i = 0; i < skillIndexTuples.Length; i++)
                {
                    if (skillIndexTuples[i].Level == level && skillIndexTuples[i].Task == task)
                        ct2.SetCoefficient(assignmentVars[i], -1);
                }
            }
        }

        return deviationVars;
    }

    private static void BuildLanguageConstraints(
        OrToolsSolver solver,
        Variable[] assignmentVars,
        Variable[] collabVars,
        (int Level, int Worker, int Task)[] skillIndexTuples,
        List<(int W1, int W2, int T)> collabIndexTuples,
        SolverParameters parameters)
    {
        var workerIndices = parameters.WorkerIndices;
        var levels = parameters.Levels;
        var collabTasks = parameters.TaskIndices
            .Where(t => parameters.TaskRequiresCollaboration[t]).ToArray();
        var workerCount = workerIndices.Length;

        int FindCollabIdx(int w1, int w2, int t)
        {
            for (int i = 0; i < collabIndexTuples.Count; i++)
            {
                if (collabIndexTuples[i].W1 == w1 && collabIndexTuples[i].W2 == w2 && collabIndexTuples[i].T == t)
                    return i;
            }
            return -1;
        }

        int FindSkillIdx(int level, int worker, int task)
        {
            for (int i = 0; i < skillIndexTuples.Length; i++)
            {
                if (skillIndexTuples[i].Level == level && skillIndexTuples[i].Worker == worker && skillIndexTuples[i].Task == task)
                    return i;
            }
            return -1;
        }

        // For each unique pair (i < j) of workers, for each collab task
        for (int wi = 0; wi < workerCount; wi++)
        {
            for (int wj = wi + 1; wj < workerCount; wj++)
            {
                foreach (var task in collabTasks)
                {
                    var collabIdx = FindCollabIdx(wi, wj, task);
                    if (collabIdx < 0) continue;

                    // sum_l x(l,wi,t) >= t(wi,wj,t)
                    var ct1 = solver.MakeConstraint(0, double.PositiveInfinity, $"collab_a_{wi}_{wj}_{task}");
                    ct1.SetCoefficient(collabVars[collabIdx], -1);
                    foreach (var l in levels)
                    {
                        var si = FindSkillIdx(l, wi, task);
                        if (si >= 0) ct1.SetCoefficient(assignmentVars[si], 1);
                    }

                    // sum_l x(l,wj,t) >= t(wi,wj,t)
                    var ct2 = solver.MakeConstraint(0, double.PositiveInfinity, $"collab_b_{wi}_{wj}_{task}");
                    ct2.SetCoefficient(collabVars[collabIdx], -1);
                    foreach (var l in levels)
                    {
                        var si = FindSkillIdx(l, wj, task);
                        if (si >= 0) ct2.SetCoefficient(assignmentVars[si], 1);
                    }

                    // sum_l (x(l,wi,t) + x(l,wj,t)) - 1 <= t(wi,wj,t)
                    var ct3 = solver.MakeConstraint(double.NegativeInfinity, 1, $"collab_c_{wi}_{wj}_{task}");
                    ct3.SetCoefficient(collabVars[collabIdx], -1);
                    foreach (var l in levels)
                    {
                        var si1 = FindSkillIdx(l, wi, task);
                        if (si1 >= 0) ct3.SetCoefficient(assignmentVars[si1], 1);
                        var si2 = FindSkillIdx(l, wj, task);
                        if (si2 >= 0) ct3.SetCoefficient(assignmentVars[si2], 1);
                    }
                }
            }
        }

        // No incompatible pairs on collab tasks
        foreach (var task in collabTasks)
        {
            var ct = solver.MakeConstraint(0, 0, $"lang_compat_task_{task}");
            for (int ci = 0; ci < collabIndexTuples.Count; ci++)
            {
                var (w1, w2, t) = collabIndexTuples[ci];
                if (t != task) continue;

                var langIdx = w1 * workerCount + w2;
                var incompatible = parameters.LanguageIncompatible[langIdx];
                if (incompatible == 1)
                {
                    ct.SetCoefficient(collabVars[ci], 1);
                }
            }
        }
    }
}
