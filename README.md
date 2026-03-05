# NOVO Planning Tool

A Blazor Server web application for generating optimized daily work schedules for NOVO Packaging & Warehousing's production department. It uses a MIP solver (Google OR-Tools) to assign workers to tasks while respecting skill levels, headcount requirements, and language collaboration constraints.

## Running locally

### Prerequisites

- .NET 10 SDK

### Start the app

From the `src/` directory:

```bash
dotnet run --project src/Novo.Planning.Web
```

Open **https://localhost:5001** in your browser.

### Run tests

```bash
dotnet test
```

## Data file format

The app imports an Excel file (`.xlsx`) with two sheets:

| Sheet | Description |
|---|---|
| **Werknemers** | Employee names, availability (days off), skill levels per task (1-4), and language (Dutch/Polish) |
| **Taken** | Task names, required headcount, minimum skill levels per level, and configuration flags |

### Excel column mapping (Taken sheet)

| Column | Field | Description |
|---|---|---|
| `Taken` | Name | Task name |
| `Aan` | IsActive | 1 = active, 0 = inactive |
| `Aantal` | HeadcountRequired | Total workers needed |
| `Aantal_min_niveau_1` | MinWorkersLevel1 | Slots requiring Expert workers |
| `Aantal_min_niveau_2` | MinWorkersLevel2 | Slots requiring Experienced workers |
| `Aantal_min_niveau_3` | MinWorkersLevel3 | Slots requiring Beginner workers |
| `Rest_min_niveau` | RestLevel | Skill level for remaining slots (default: 3/Beginner) |
| `Verdeling oud planbord` | BoardPosition | 2 = Right side, else Left side |
| `Samenwerken` | RequiresLanguageCollaboration | 1 = workers must share a language |

---

## Task Assignment System

### Skill Levels

```
Expert     = 1  (highest skill)
Experienced = 2
Beginner    = 3
Cannot      = 4  (ineligible — worker cannot perform this task)
```

Lower number = higher skill. A worker is eligible to fill a slot at level L if their actual skill level for that task is ≤ L. For example, an Expert (1) can fill Expert, Experienced, or Beginner slots. A Beginner (3) can only fill Beginner slots.

### How Task Slot Allocation Works

Each task defines:
- **HeadcountRequired** — total number of workers needed
- **MinWorkersLevel1** — number of slots that must be filled by **Expert** workers
- **MinWorkersLevel2** — number of slots that must be filled by **Experienced-or-better** workers
- **MinWorkersLevel3** — number of slots that must be filled by **Beginner-or-better** workers
- **RestLevel** — skill level used for remaining slots (default: Beginner)

**The levels are additive slots, not cumulative counts:**

```
HeadcountRequired = MinWorkersLevel1 + MinWorkersLevel2 + MinWorkersLevel3 + remainder
```

The `remainder` is `HeadcountRequired - (MinL1 + MinL2 + MinL3)` and these slots are filled at `RestLevel`.

#### Example

Task "Ompakken" with HeadcountRequired=5, MinWorkersLevel1=1, MinWorkersLevel2=0, MinWorkersLevel3=0, RestLevel=Beginner:

| Slot | Level | Who can fill it |
|---|---|---|
| 1 | Expert (1) | Only workers with skill = Expert |
| 2-5 | Beginner (3) via RestLevel | Workers with skill ≤ 3 (Expert, Experienced, or Beginner) |

The solver allocates exactly 1 Expert slot + 4 Beginner-level slots.

#### Example with Level 2

Task with HeadcountRequired=5, MinWorkersLevel1=1, MinWorkersLevel2=2, MinWorkersLevel3=0, RestLevel=Beginner:

| Slot | Level | Who can fill it |
|---|---|---|
| 1 | Expert (1) | Only Expert workers |
| 2-3 | Experienced (2) | Expert or Experienced workers |
| 4-5 | Beginner (3) via RestLevel | Expert, Experienced, or Beginner workers |

### Current Data Patterns

In production Excel data and test fixtures, **MinWorkersLevel2 and MinWorkersLevel3 are always 0**. Only MinWorkersLevel1 (Expert minimum) is used in practice. Most tasks look like:

```
HeadcountRequired = 5
MinWorkersLevel1  = 1   (need 1 expert)
MinWorkersLevel2  = 0
MinWorkersLevel3  = 0
RestLevel         = 3   (remaining 4 slots at Beginner level)
```

---

## Solver Constraints

The solver (Google OR-Tools CBC MIP) uses these constraints:

### Hard Constraints

| # | Constraint | Description |
|---|---|---|
| 1 | Worker uniqueness | Each worker is assigned to exactly 1 task |
| 2 | Skill eligibility | Worker can only fill slot at level L if their skill ≤ L |
| 3 | Headcount | Each task gets exactly HeadcountRequired workers |
| 4a | Min Level 1 | At least MinWorkersLevel1 workers assigned at **exactly** Level 1 (per-level, not cumulative) |
| 4b | Max Level 3 | At most MinWorkersLevel3 workers assigned at **exactly** Level 3 (limits beginners) |
| 5 | Language | Workers on collaboration tasks must share a common language |

**Important:** There is no hard constraint for Level 2 minimums. Level 2 requirements are only enforced through the objective function as a soft penalty (deviation variables).

### Skill Level Modes

The solver tries progressively relaxed constraint modes:

| Mode | Min L1 enforced | Max L3 enforced |
|---|---|---|
| `"full"` | Yes | Yes |
| `"partial"` | Yes | No |
| `"none"` | No | No |

### Relaxation Attempts

If the solver cannot find a feasible solution, it progressively relaxes constraints:

| Attempt | Language | Skill Mode | Warning if used |
|---|---|---|---|
| 0 | Yes | full | *(none — optimal)* |
| 1 | No | full | "Werknemers spreken niet overal dezelfde taal" |
| 2 | Yes | partial | "Niet voldaan aan minimum eisen van niveau 2 en 3" |
| 3 | Yes | none | "Niet aan minimum eisen van de niveaus voldaan" |
| 4 | No | none | Both warnings above |

If all 5 attempts fail → `Infeasible` (no valid assignment exists with available workers and tasks).

### Optimization Strategies

| Strategy | Goal |
|---|---|
| MaximizeExpertise | Maximize expert assignments (Level 1 = 1.0, Level 2 = 0.5) |
| LearningFocused | Minimize deviation from target slot distribution |
| Hybrid | Balance expertise with slot distribution targets |

---

## Compliance Validator

The compliance validator (`ComplianceValidator.cs`) runs independently from the solver to check manual or generated plannings against business rules.

### Checks performed

| Check | Severity | Description |
|---|---|---|
| Headcount | Error | Each task has enough workers assigned |
| Skill minimums | Warning | Minimum skill level requirements per task |
| Language collaboration | Warning | Workers on collaboration tasks share a language |
| Worker overallocation | Error | No worker assigned to multiple tasks |
| Skill eligibility | Error | No Cannot (level 4) workers assigned |

### Skill Minimum Counting — Cumulative

The validator counts skill levels **cumulatively** (or-better):

```
Level1Count = workers with skill ≤ Expert      → only Experts
Level2Count = workers with skill ≤ Experienced → Experts + Experienced
Level3Count = workers with skill ≤ Beginner    → Experts + Experienced + Beginners
```

An Expert worker counts toward **all three** level counts.

---

## Known Design Inconsistency: Solver vs Validator

The solver and compliance validator use **different counting methods** for skill levels:

| Component | Counting method | Level 1 = 1, Level 2 = ? |
|---|---|---|
| **Solver** (ModelBuilder) | Per-level only | Level 2 count = workers assigned at exactly Level 2 |
| **Validator** (ComplianceValidator) | Cumulative (or-better) | Level 2 count = workers at Level 1 + Level 2 |

### Why it hasn't caused issues

In practice, MinWorkersLevel2 and MinWorkersLevel3 are always 0. With only MinWorkersLevel1 used:
- Per-level count of Level 1 workers = cumulative count of Level 1 workers (Expert is the highest level, both methods agree)

### When it would matter

If a task had `MinWorkersLevel2=3`, the solver would require 3 workers assigned at **exactly** Experienced level, while the validator would check for 3 workers at Experienced **or better** (Experts would count too). This means:
- Solver: 1 Expert + 2 Experienced = Expert check passes, but Experienced slots specifically need 3 (would need 3 non-expert experienced workers)
- Validator: 1 Expert + 2 Experienced = 3 workers at ≤ Experienced → passes

**If Level 2/3 requirements are ever introduced in production data, this inconsistency should be resolved first.**

### Related code quirk

In `SolverParameterBuilder.cs` line 133:
```csharp
var maxLevel3PerTask = activeTasks.Select(t => t.MinWorkersLevel3).ToArray();
```

`MinWorkersLevel3` is reused as `MaxLevel3PerTask` — the same value serves as both minimum (via RestLevel allocation) and maximum (via Constraint 4b). This works when the value is 0 or when it exactly matches the intended beginner count, but the naming is confusing.

---

## UI Display (Kanban Board)

### Color Legend

| Color | Skill Level |
|---|---|
| Green | Expert |
| Blue | Experienced |
| Yellow | Beginner |
| Red | Cannot |

### Task Column Badges

Each task column shows:
- **Headcount**: `assigned/required` (e.g., `3/5`) — turns red when under-staffed
- **Requirement badges**: only shown when a MinWorkersLevel > 0
  - `min. 1 E — voldaan` (green badge, requirement met)
  - `min. 1 E — nog 1 nodig` (red outline badge, requirement not met)
- **Composition text**: `(1 E, 1 Er, 3 B)` — informational breakdown of assigned workers

### Task Highlighting

Clicking a task header highlights all worker badges across the board by their skill level for that task (green/blue/yellow/red). Click outside to deselect.

### Drag-and-Drop

All drag-and-drop is handled in **pure JavaScript** (not Blazor events) for reliability with Blazor Server's SignalR round-trips:
- `dragstart` → stores worker/source or column data in `window.novoDragDrop`
- `dragover` → global handler allows drops on elements with `data-drop-task`, `data-drop-zone`, `data-drop-side`
- `drop` → global handler reads target data attributes and calls `[JSInvokable]` .NET methods via `DotNetObjectReference`

Drop rules:
- Workers with skill = Cannot are rejected when dropped on a task
- Absent workers can only be dragged to "Niet ingepland", not to task columns
- Custom tasks accept any worker (no skill data exists)

---

## Project Structure

```
src/
├── src/
│   ├── Novo.Planning.Domain/          # Models (TaskDefinition, Person, SkillLevel, etc.)
│   ├── Novo.Planning.Infrastructure/  # Excel import, repositories
│   ├── Novo.Planning.Solver/          # OR-Tools MIP solver
│   │   ├── SolverParameterBuilder.cs  # Builds slot allocation from TaskDefinitions
│   │   ├── ModelBuilder.cs            # Builds MIP constraints and objective
│   │   └── SolverService.cs           # Orchestrates solve with relaxation attempts
│   └── Novo.Planning.Web/            # Blazor Server UI
│       └── Features/Planning/
│           ├── Components/            # KanbanBoard, KanbanColumn, KanbanCard, UnassignedPool
│           ├── Pages/                 # PlanningEditor, PlanningDashboard
│           └── Services/             # ComplianceValidator
└── tests/
    ├── Novo.Planning.Domain.Tests/
    ├── Novo.Planning.Infrastructure.Tests/
    ├── Novo.Planning.Solver.Tests/     # Includes realistic production-scale fixtures
    └── Novo.Planning.Web.Tests/
```
