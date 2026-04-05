using Novo.Planning.Domain.Models;

namespace Novo.Planning.Web.Features.Planning.Services;

public class HeuristicPlanningGenerator : IPlanningGeneratorService
{
    public List<PlanningAssignment> Generate(List<Person> availableWorkers, List<TaskDefinition> selectedTasks)
    {
        var rng = new Random();
        var assignments = new List<PlanningAssignment>();
        var assigned = new HashSet<string>();

        var taskCandidates = new Dictionary<string, List<(Person Worker, SkillLevel Level)>>();
        foreach (var task in selectedTasks)
        {
            taskCandidates[task.Name] = availableWorkers
                .Select(w => (Worker: w, Level: w.Skills.GetValueOrDefault(task.Name, SkillLevel.Cannot)))
                .Where(c => c.Level != SkillLevel.Cannot)
                .OrderBy(c => c.Worker.IsTempWorker ? 1 : 0)
                .ThenBy(_ => rng.Next())
                .ToList();
        }

        foreach (var task in selectedTasks)
        {
            var candidates = taskCandidates[task.Name];
            var taskAssigned = new List<(Person Worker, SkillLevel Level)>();

            FillSlots(candidates, taskAssigned, assigned, task.MinWorkersLevel1,
                c => c.Level == SkillLevel.Expert);

            var level2Have = taskAssigned.Count(a => a.Level <= SkillLevel.Experienced);
            FillSlots(candidates, taskAssigned, assigned, task.MinWorkersLevel2 - level2Have,
                c => c.Level <= SkillLevel.Experienced);

            var level3Have = taskAssigned.Count(a => a.Level <= SkillLevel.Beginner);
            FillSlots(candidates, taskAssigned, assigned, task.MinWorkersLevel3 - level3Have,
                c => c.Level <= SkillLevel.Beginner);

            var remaining = task.HeadcountRequired - taskAssigned.Count;
            if (remaining > 0)
            {
                var rest = candidates
                    .Where(c => !assigned.Contains(c.Worker.Id))
                    .OrderBy(c => (int)c.Level)
                    .ThenBy(c => c.Worker.IsTempWorker ? 1 : 0)
                    .ThenBy(_ => rng.Next())
                    .Take(remaining);
                foreach (var c in rest)
                {
                    taskAssigned.Add(c);
                    assigned.Add(c.Worker.Id);
                }
            }

            if (task.RequiresLanguageCollaboration && taskAssigned.Count >= 2)
            {
                TryFixLanguageCollaboration(taskAssigned, candidates, assigned);
            }

            foreach (var (worker, level) in taskAssigned)
            {
                assignments.Add(new PlanningAssignment
                {
                    TaskName = task.Name,
                    WorkerName = worker.Name,
                    SkillLevel = level
                });
            }
        }

        return assignments;
    }

    private static void TryFixLanguageCollaboration(
        List<(Person Worker, SkillLevel Level)> taskAssigned,
        List<(Person Worker, SkillLevel Level)> candidates,
        HashSet<string> assigned)
    {
        var allShareLanguage = taskAssigned.All(a => a.Worker.SpeaksDutch) ||
                               taskAssigned.All(a => a.Worker.SpeaksPolish);

        if (allShareLanguage) return;

        var majorityDutch = taskAssigned.Count(a => a.Worker.SpeaksDutch) >=
                            taskAssigned.Count(a => a.Worker.SpeaksPolish);

        for (int i = 0; i < taskAssigned.Count; i++)
        {
            var w = taskAssigned[i];
            var matches = majorityDutch ? w.Worker.SpeaksDutch : w.Worker.SpeaksPolish;
            if (matches) continue;

            var swap = candidates
                .Where(c => !assigned.Contains(c.Worker.Id) &&
                            (majorityDutch ? c.Worker.SpeaksDutch : c.Worker.SpeaksPolish))
                .OrderBy(c => (int)c.Level)
                .ThenBy(c => c.Worker.IsTempWorker ? 1 : 0)
                .FirstOrDefault();
            if (swap.Worker != null)
            {
                assigned.Remove(w.Worker.Id);
                assigned.Add(swap.Worker.Id);
                taskAssigned[i] = swap;
            }
        }
    }

    private static void FillSlots(
        List<(Person Worker, SkillLevel Level)> candidates,
        List<(Person Worker, SkillLevel Level)> taskAssigned,
        HashSet<string> assigned,
        int needed,
        Func<(Person Worker, SkillLevel Level), bool> filter)
    {
        if (needed <= 0) return;
        var picks = candidates
            .Where(c => !assigned.Contains(c.Worker.Id) && filter(c))
            .Take(needed);
        foreach (var pick in picks)
        {
            taskAssigned.Add(pick);
            assigned.Add(pick.Worker.Id);
        }
    }
}
