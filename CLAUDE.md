# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NOVO Planning Generator — a Streamlit web app that generates optimized daily work schedules for NOVO Packaging & Warehousing's production department. Written in Python, with a Dutch-language UI. The entire application lives in a single file: `tool/planningstool_code.py`.

## Development Commands

```bash
# Install dependencies
pip install -r tool/requirements.txt

# Run the application (serves on port 8501)
streamlit run tool/planningstool_code.py

# Run with CORS/XSRF disabled (as in devcontainer)
streamlit run tool/planningstool_code.py --server.enableCORS false --server.enableXsrfProtection false
```

There are no tests, linting, or CI/CD pipelines configured.

## Dev Container

The `.devcontainer/devcontainer.json` targets Python 3.13 on Debian Bookworm. It auto-installs requirements and launches the Streamlit server on attach.

## Architecture

The app is a monolithic Streamlit script (~980 lines) with this flow:

1. **Data upload** — User uploads an Excel file with three sheets:
   - `Werknemers` (employees): names, availability, skill levels per task, language (Dutch/Polish)
   - `Taken` (tasks): task names, required headcount, minimum skill levels
   - `Uitzendkracht` (temp workers): default skill profile for agency staff

2. **Interactive configuration** (Streamlit widgets) — Select day, toggle employee attendance, add/remove tasks, pin specific employees to tasks, set headcounts via AgGrid, add one-off tasks, configure Zeelandia loading/unloading.

3. **MIP optimization** — Builds a binary integer program using the `pulp` library (CBC solver):
   - **Decision variables**: `X[level, worker, task]` — binary assignment
   - **Constraints**: one task per worker, skill level eligibility, exact headcount per task, minimum skill levels, language compatibility (Dutch/Polish speakers can collaborate)
   - **Three objective functions** selectable by user:
     1. Maximize skill match (best person on each task)
     2. Minimize deviation from minimums (learning-oriented)
     3. Hybrid (experts on critical tasks, beginners elsewhere)
   - **Fallback**: if infeasible, progressively relaxes constraints (language → partial skill levels → all skill levels) and warns the user

4. **Excel output** — Generates a formatted `.xlsx` with xlsxwriter: tasks split into left/right board sections, color-coded rows, absent employees list, comments area, NOVO branding. Special "Frikandellen-Vrijdag" feature on Fridays.

## Key Domain Terminology (Dutch)

| Dutch | English |
|---|---|
| Werknemers | Employees |
| Taken | Tasks |
| Uitzendkracht | Temp/agency worker |
| Aanwezig | Present/available |
| Niveau | Skill level (1=best, 4=cannot do) |
| Samenwerken | Collaboration (language constraint) |
| Dagplanning | Daily schedule |
| Doelfunctie | Objective function |

## Important Notes

- All variable names, comments, and UI strings are in Dutch
- Skill levels: 1 (expert) through 4 (cannot perform); levels 1-3 are assignable, level 4 means ineligible
- The solver timeout is 300 seconds
- External images (NOVO logo, Friday image) are loaded from GitHub URLs at runtime — requires internet
- The `pulp` package (>= 2.7.0) provides the CBC solver with multi-arch binary support
