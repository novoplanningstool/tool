using Novo.Planning.Domain.Models;

namespace Novo.Planning.Solver;

/// <summary>
/// Builds SolverParameters from domain models.
/// Port of build_solver_parameters() from solver.py.
/// </summary>
public static class SolverParameterBuilder
{
    public static SolverParameters Build(List<Person> presentWorkers, List<TaskDefinition> activeTasks)
    {
        var levels = new[] { 1, 2, 3 };
        var workerCount = presentWorkers.Count;
        var taskCount = activeTasks.Count;
        var workerIndices = Enumerable.Range(0, workerCount).ToArray();
        var taskIndices = Enumerable.Range(0, taskCount).ToArray();

        var workerNames = presentWorkers.Select(w => w.Name).ToArray();
        var taskNames = activeTasks.Select(t => t.Name).ToArray();

        // Build skill matrix: skill_matrix[worker, task] = skill level (1-4)
        var skillMatrix = new int[workerCount, taskCount];
        for (int w = 0; w < workerCount; w++)
        {
            for (int t = 0; t < taskCount; t++)
            {
                var taskName = taskNames[t];
                skillMatrix[w, t] = presentWorkers[w].Skills.TryGetValue(taskName, out var level)
                    ? (int)level
                    : 4; // default to Cannot
            }
        }

        // Build skill index tuples and eligibility array
        // Order: for each level, for each worker, for each task (matches Python's product(levels, werknemers, taken))
        var skillIndexTuples = new (int Level, int Worker, int Task)[levels.Length * workerCount * taskCount];
        var skillEligible = new int[skillIndexTuples.Length];
        var idx = 0;
        foreach (var level in levels)
        {
            foreach (var worker in workerIndices)
            {
                foreach (var task in taskIndices)
                {
                    skillIndexTuples[idx] = (level, worker, task);
                    // Worker is eligible at level L if their actual skill <= L
                    skillEligible[idx] = skillMatrix[worker, task] <= level ? 1 : 0;
                    idx++;
                }
            }
        }

        // Build teamsize index tuples and min workers per level
        var teamsizeIndexTuples = new (int Level, int Task)[levels.Length * taskCount];
        var minWorkersPerLevel = new int[teamsizeIndexTuples.Length];
        idx = 0;
        foreach (var level in levels)
        {
            foreach (var task in taskIndices)
            {
                teamsizeIndexTuples[idx] = (level, task);
                minWorkersPerLevel[idx] = level switch
                {
                    1 => activeTasks[task].MinWorkersLevel1,
                    2 => activeTasks[task].MinWorkersLevel2,
                    3 => activeTasks[task].MinWorkersLevel3,
                    _ => 0
                };
                idx++;
            }
        }

        // Process remaining skill levels (port of process_remaining_skill_levels)
        // Distribute unallocated workers to the Rest_min_niveau column
        for (int t = 0; t < taskCount; t++)
        {
            var task = activeTasks[t];
            var scheduled = task.MinWorkersLevel1 + task.MinWorkersLevel2 + task.MinWorkersLevel3;
            if (task.HeadcountRequired > scheduled)
            {
                var remaining = task.HeadcountRequired - scheduled;
                var restLevel = (int)task.RestLevel;
                // Find the teamsize index for (restLevel, t) and add remaining
                for (int i = 0; i < teamsizeIndexTuples.Length; i++)
                {
                    if (teamsizeIndexTuples[i].Level == restLevel && teamsizeIndexTuples[i].Task == t)
                    {
                        minWorkersPerLevel[i] += remaining;
                        break;
                    }
                }
            }
        }

        // Build language compatibility
        var workerSpeaksDutch = presentWorkers.Select(w => w.SpeaksDutch).ToArray();
        var workerSpeaksPolish = presentWorkers.Select(w => w.SpeaksPolish).ToArray();

        var languageIndexTuples = new (int Worker1, int Worker2)[workerCount * workerCount];
        var languageIncompatible = new int[languageIndexTuples.Length];
        idx = 0;
        for (int w1 = 0; w1 < workerCount; w1++)
        {
            for (int w2 = 0; w2 < workerCount; w2++)
            {
                languageIndexTuples[idx] = (w1, w2);
                // Default incompatible (1), set to 0 if compatible
                languageIncompatible[idx] = 1;
                idx++;
            }
        }

        // Set compatible pairs (matching Python logic: only upper triangle w1 < w2)
        for (int w1 = 0; w1 < workerCount - 1; w1++)
        {
            for (int w2 = w1; w2 < workerCount; w2++)
            {
                bool dutchShared = workerSpeaksDutch[w1] && workerSpeaksDutch[w2];
                bool polishShared = workerSpeaksPolish[w1] && workerSpeaksPolish[w2];
                if (dutchShared || polishShared)
                {
                    var langIdx = w1 * workerCount + w2;
                    languageIncompatible[langIdx] = 0;
                }
            }
        }

        // Build task-level arrays
        var taskHeadcounts = activeTasks.Select(t => t.HeadcountRequired).ToArray();
        var taskRequiresCollab = activeTasks.Select(t => t.RequiresLanguageCollaboration).ToArray();
        var minLevel1PerTask = activeTasks.Select(t => t.MinWorkersLevel1).ToArray();
        var maxLevel3PerTask = activeTasks.Select(t => t.MinWorkersLevel3).ToArray();

        return new SolverParameters
        {
            Levels = levels,
            TaskIndices = taskIndices,
            WorkerIndices = workerIndices,
            TaskNames = taskNames,
            WorkerNames = workerNames,
            SkillMatrix = skillMatrix,
            SkillIndexTuples = skillIndexTuples,
            SkillEligible = skillEligible,
            TeamsizeIndexTuples = teamsizeIndexTuples,
            MinWorkersPerLevel = minWorkersPerLevel,
            LanguageIndexTuples = languageIndexTuples,
            LanguageIncompatible = languageIncompatible,
            TaskHeadcounts = taskHeadcounts,
            TaskRequiresCollaboration = taskRequiresCollab,
            MinLevel1PerTask = minLevel1PerTask,
            MaxLevel3PerTask = maxLevel3PerTask,
            WorkerSpeaksDutch = workerSpeaksDutch,
            WorkerSpeaksPolish = workerSpeaksPolish,
        };
    }
}
