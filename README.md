# NOVO Planning Generator

A Streamlit web app that generates optimized daily work schedules for NOVO Packaging & Warehousing's production department. Upload an Excel file with employee and task data, configure the day's parameters, and the app produces a color-coded planning sheet using a MIP solver.

## Running locally

### Prerequisites

- Python 3.11 (mip==1.15.0 requires >=3.7,<3.12)

### Install dependencies

From the `tool/` directory:

```bash
pip install -r requirements.txt
```

### Start the app

```bash
python -m streamlit run planningstool_code.py
```

Open **http://localhost:8501** in your browser. Press **Ctrl+C** to stop.

## Running with Docker

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed and running

### Start the app

From the `tool/` directory:

```bash
docker run --rm -p 8501:8501 -v "$(pwd):/workspace" -w /workspace \
  mcr.microsoft.com/devcontainers/python:1-3.13-bookworm \
  bash -c "pip3 install --user -r requirements.txt && streamlit run planningstool_code.py --server.enableCORS false --server.enableXsrfProtection false --server.address 0.0.0.0"
```

Once you see `You can now view your Streamlit app in your browser`, open **http://localhost:8501** in your browser.

Press **Ctrl+C** in the terminal to stop the container.

## Running via GitHub Codespaces

Open this repository in a GitHub Codespace. Dependencies install automatically and the Streamlit server starts on attach. The app preview opens on port 8501.

## Data file format

The app expects an Excel file (`.xlsx`) with three sheets:

| Sheet | Description |
|---|---|
| **Werknemers** | Employee names, availability, skill levels per task (1–4), and language (Dutch/Polish) |
| **Taken** | Task names, required headcount, and minimum skill levels |
| **Uitzendkracht** | Default skill profile for temp/agency workers |

Skill levels: **1** = expert, **2** = experienced, **3** = beginner, **4** = cannot perform (ineligible).
