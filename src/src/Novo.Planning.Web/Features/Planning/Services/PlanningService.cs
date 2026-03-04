using Novo.Planning.Domain.Interfaces;
using Novo.Planning.Domain.Models;
using Novo.Planning.Solver;

namespace Novo.Planning.Web.Features.Planning.Services;

public class PlanningService : IPlanningService
{
    private const string TempWorkerTemplateId = "temp-worker-template";

    private readonly IPersonRepository _personRepository;
    private readonly ITaskDefinitionRepository _taskRepository;
    private readonly IPlanningRepository _planningRepository;
    private readonly ISolverService _solverService;
    private readonly IExcelExportService _excelExportService;

    public PlanningService(
        IPersonRepository personRepository,
        ITaskDefinitionRepository taskRepository,
        IPlanningRepository planningRepository,
        ISolverService solverService,
        IExcelExportService excelExportService)
    {
        _personRepository = personRepository;
        _taskRepository = taskRepository;
        _planningRepository = planningRepository;
        _solverService = solverService;
        _excelExportService = excelExportService;
    }

    public async Task<SolverResult> GeneratePlanningAsync(PlanningSettings settings)
    {
        var (presentWorkers, activeTasks) = await ResolveWorkersAndTasks(settings);
        return _solverService.Solve(presentWorkers, activeTasks, settings.Strategy);
    }

    public async Task<SolverResult> ValidateAssignmentsAsync(PlanningSettings settings, List<PlanningAssignment> assignments)
    {
        var (presentWorkers, activeTasks) = await ResolveWorkersAndTasks(settings);
        return _solverService.ValidateAssignments(presentWorkers, activeTasks, assignments);
    }

    private async Task<(List<Person> PresentWorkers, List<TaskDefinition> ActiveTasks)> ResolveWorkersAndTasks(PlanningSettings settings)
    {
        var allPersons = await _personRepository.GetAllAsync();
        var allTasks = await _taskRepository.GetAllAsync();

        var presentWorkers = allPersons
            .Where(p => settings.PresentWorkerIds.Contains(p.Id))
            .ToList();

        if (settings.TempWorkerCount > 0)
        {
            var template = allPersons.FirstOrDefault(p => p.Id == TempWorkerTemplateId);
            if (template != null)
            {
                for (int i = 1; i <= settings.TempWorkerCount; i++)
                {
                    var tempWorker = new Person
                    {
                        Id = $"temp-worker-{i}",
                        Name = $"Uitzendkracht {i}",
                        SpeaksDutch = template.SpeaksDutch,
                        SpeaksPolish = template.SpeaksPolish,
                        DefaultDaysOff = [..template.DefaultDaysOff],
                        Skills = new Dictionary<string, SkillLevel>(template.Skills)
                    };
                    presentWorkers.Add(tempWorker);
                }
            }
        }

        var activeTasks = allTasks
            .Where(t => settings.ActiveTaskIds.Contains(t.Id))
            .Select(t =>
            {
                if (settings.TaskHeadcountOverrides.TryGetValue(t.Id, out var headcountOverride))
                {
                    return new TaskDefinition
                    {
                        Id = t.Id,
                        Name = t.Name,
                        IsActive = t.IsActive,
                        HeadcountRequired = headcountOverride,
                        BoardPosition = t.BoardPosition,
                        MinWorkersLevel1 = t.MinWorkersLevel1,
                        MinWorkersLevel2 = t.MinWorkersLevel2,
                        MinWorkersLevel3 = t.MinWorkersLevel3,
                        RestLevel = t.RestLevel,
                        RequiresLanguageCollaboration = t.RequiresLanguageCollaboration,
                        SortOrder = t.SortOrder
                    };
                }
                return t;
            })
            .ToList();

        return (presentWorkers, activeTasks);
    }

    public async Task SavePlanningAsync(PlanningModel planning)
    {
        await _planningRepository.UpsertAsync(planning);
    }

    public async Task<PlanningModel?> LoadPlanningAsync(string id)
    {
        return await _planningRepository.GetByIdAsync(id);
    }

    public async Task<IReadOnlyList<PlanningModel>> GetAllPlanningsAsync()
    {
        return await _planningRepository.GetAllAsync();
    }

    public async Task<IReadOnlyList<PlanningModel>> GetTemplatesAsync()
    {
        return await _planningRepository.GetTemplatesAsync();
    }

    public async Task DeletePlanningAsync(string id)
    {
        await _planningRepository.DeleteAsync(id);
    }

    public async Task<byte[]?> ExportToExcelAsync(string planningId)
    {
        var planning = await _planningRepository.GetByIdAsync(planningId);
        if (planning == null)
            return null;

        return _excelExportService.Export(planning);
    }
}
