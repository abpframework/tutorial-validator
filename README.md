# TutorialValidator

[![CI](https://github.com/AbpFramework/TutorialValidator/actions/workflows/ci.yml/badge.svg)](https://github.com/AbpFramework/TutorialValidator/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

An AI-powered automated testing system that validates software documentation tutorials by simulating a real developer following each step.

If a tutorial step fails, it means the documentation has a bug.

> **Current status:** The system was built and validated against [ABP Framework](https://abp.io) tutorials. The architecture is designed to be tutorial-agnostic — support for general software tutorials (any framework, any stack) is actively being expanded.

---

## How It Works

TutorialValidator runs a three-phase pipeline:

```
Tutorial URL
    │
    ▼
┌──────────────────────────────────┐
│  Analyst                         │
│  1. Scrape tutorial pages (HTML) │
│  2. AI extracts structured steps │
│  3. Output: testplan.json        │
└──────────────┬───────────────────┘
               │ testplan.json
               ▼
┌──────────────────────────────────┐
│  Executor                        │
│  AI agent simulates a developer  │
│  Runs commands, writes files,    │
│  makes HTTP calls, asserts state │
│  Output: results/ + summary.json │
└──────────────┬───────────────────┘
               │ summary.json
               ▼
┌──────────────────────────────────┐
│  Reporter                        │
│  Sends HTML email report         │
│  Posts Discord notification      │
└──────────────────────────────────┘
```

The Orchestrator coordinates all three phases and manages the Docker environment that isolates tutorial execution.

---

## Prerequisites

| Tool | Install |
|---|---|
| .NET 10 SDK | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Docker Desktop | [docker.com/get-started](https://www.docker.com/get-started/) |
| OpenAI or Azure OpenAI API key | [platform.openai.com](https://platform.openai.com) or Azure portal |

> **Tutorial-specific tools:** Some tutorials require additional tooling in the execution environment (e.g., a specific CLI, a database engine, a runtime). The Docker-based execution mode lets you customize the container image to include whatever tools your target tutorials need.

---

## Quick Start

**1. Clone the repository**

```bash
git clone https://github.com/AbpFramework/TutorialValidator.git
cd TutorialValidator
```

**2. Configure environment**

```bash
cp docker/.env.example docker/.env
```

Open `docker/.env` and set your API key:

```env
# For OpenAI:
OPENAI_API_KEY=sk-...

# Or for Azure OpenAI:
# AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
# AZURE_OPENAI_API_KEY=your-key
# AZURE_OPENAI_DEPLOYMENT=gpt-4o
```

**3. Run a full validation**

```bash
dotnet run --project src/Validator.Orchestrator -- run \
  --url "https://your-docs-site.com/tutorials/getting-started" \
  --output ./output
```

The example above uses a placeholder URL. Point `--url` at any publicly accessible tutorial page. The Analyst will scrape the page (and linked pages in the same tutorial series) and extract an executable test plan.

**4. Inspect results**

```
output/
├── scraped/          # Raw scraped tutorial content
├── testplan.json     # Structured test plan extracted by the Analyst
├── results/          # Step-by-step execution results
├── logs/             # Full execution logs
└── summary.json      # Pass/fail summary with diagnostics
```

An HTML email report is sent (if email is configured) and a Discord notification is posted (if a webhook is configured).

---

## Developer Personas

The Executor simulates a developer at a specific experience level. The persona controls how strictly the agent follows instructions and whether it can self-correct errors.

| Persona | Technical Knowledge | Framework Knowledge | On Error | Use Case |
|---|---|---|---|---|
| `junior` | Basic programming | None | Reports failure immediately | Strictest test — catches even small doc gaps |
| `mid` | Familiar with the relevant stack | None | Reports failure | Default — realistic new user following the tutorial |
| `senior` | Expert in the relevant stack | Expert | Self-diagnoses, retries up to 3× | Validates the happy path; notes any fixes needed |

Set the persona with `--persona <level>`:

```bash
dotnet run --project src/Validator.Orchestrator -- run \
  --url "https://your-docs-site.com/tutorials/getting-started" \
  --persona senior
```

---

## Step Types

The Analyst extracts tutorial content into four structured step types, which the Executor then runs:

| Type | Description | Example |
|---|---|---|
| `Command` | CLI command execution | `npm install`, `dotnet build`, `git clone` |
| `FileOperation` | Create, read, modify, or delete a file or directory | Create a config file in the project root |
| `CodeChange` | Find-and-replace or semantic code modification in an existing file | Add a method to an existing class |
| `Expectation` | Assertion — verifies a build, HTTP response, or application state | HTTP GET `/health` returns `200 OK` |

A test plan is a JSON array of these steps. See [`samples/testplan.json`](samples/testplan.json) for a full example (based on an ABP Framework tutorial).

---

## Sub-components

| Project | Role |
|---|---|
| `Validator.Core` | Shared models, enums, and result types used by all other projects |
| `Validator.Analyst` | Scrapes tutorial HTML, converts to Markdown, uses AI to extract a structured test plan |
| `Validator.Executor` | AI agent that executes test plan steps inside an isolated working directory |
| `Validator.Orchestrator` | Top-level CLI: coordinates Analyst → Executor → Reporter, manages Docker lifecycle |
| `Validator.Reporter` | Formats and sends HTML email reports and Discord notifications |

---

## Running Individual Components

### Generate a test plan only (Analyst)

```bash
dotnet run --project src/Validator.Analyst -- full \
  --url "https://your-docs-site.com/tutorials/getting-started" \
  --output ./output
```

### Execute an existing test plan only (Executor)

```bash
dotnet run --project src/Validator.Executor -- run \
  --input ./output/testplan.json \
  --workdir ./workspace \
  --output ./output/results \
  --persona mid
```

### Full pipeline — local mode (no Docker)

```bash
dotnet run --project src/Validator.Orchestrator -- run \
  --url "https://your-docs-site.com/tutorials/getting-started" \
  --output ./output \
  --local
```

### Full pipeline — Docker mode (isolated environment + Executor container)

```bash
dotnet run --project src/Validator.Orchestrator -- run \
  --url "https://your-docs-site.com/tutorials/getting-started" \
  --output ./output
```

### Run Executor in Docker with an existing test plan

```bash
dotnet run --project src/Validator.Orchestrator -- docker-only \
  --testplan ./output/testplan.json \
  --output ./output
```

### All Orchestrator options

| Flag | Default | Description |
|---|---|---|
| `--url`, `-u` | — | Tutorial URL to validate |
| `--testplan`, `-t` | — | Path to an existing `testplan.json` |
| `--skip-analyst` | false | Skip scraping, use an existing test plan |
| `--output`, `-o` | `./output` | Output directory |
| `--persona` | `mid` | Developer persona: `junior`, `mid`, `senior` |
| `--local` | false | Run Executor locally instead of in Docker |
| `--keep-containers` | false | Keep Docker containers running after completion |
| `--timeout` | `60` | Timeout in minutes |
| `--config`, `-c` | — | Path to a custom `appsettings.json` |

---

## Configuration Reference

### `appsettings.json`

```json
{
  "AI": {
    "Provider": "OpenAI",
    "Model": "gpt-4o",
    "ApiKey": ""
  },
  "Docker": {
    "ComposeFile": "../docker/docker-compose.yml",
    "SqlServerPassword": "YourStrong!Password123"
  },
  "Orchestrator": {
    "DefaultOutputPath": "./output",
    "KeepContainersAfterRun": false,
    "TimeoutMinutes": 60
  },
  "Email": {
    "Enabled": false,
    "SmtpHost": "localhost",
    "SmtpPort": 587,
    "UseSsl": true,
    "Username": "",
    "Password": "",
    "FromAddress": "tutorial-validator@example.com",
    "FromName": "Tutorial Validator",
    "ToAddresses": ["your-email@example.com"]
  },
  "Discord": {
    "Enabled": false,
    "WebhookUrl": ""
  }
}
```

> **Note on Docker configuration:** The default `docker-compose.yml` includes a SQL Server sidecar, which was used for the initial ABP Framework tutorial validation. For tutorials targeting a different database or runtime, update `docker/docker-compose.yml` and `Dockerfile` to include the services your tutorials require.

### Environment variable overrides

Environment variables always take precedence over `appsettings.json` values.

| Variable | Description | Default |
|---|---|---|
| `OPENAI_API_KEY` | OpenAI API key | — |
| `OPENAI_MODEL` | OpenAI model name | `gpt-4o` |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI endpoint URL | — |
| `AZURE_OPENAI_API_KEY` | Azure OpenAI API key | — |
| `AZURE_OPENAI_DEPLOYMENT` | Azure OpenAI deployment name | `gpt-4o` |
| `AI_PROVIDER` | Force `OpenAI` or `AzureOpenAI` (auto-detected if omitted) | — |
| `Discord__Enabled` | Enable Discord notifications (`true`/`false`) | `false` |
| `Discord__WebhookUrl` | Discord incoming webhook URL | — |
| `EXECUTOR_BUILD_GATE_INTERVAL` | Senior persona: run `dotnet build` every N steps (0 = disabled) | `0` |
| `ConnectionStrings__Default` | SQL Server connection string (used inside Docker) | — |
| `EXECUTOR_WORKDIR` | Working directory for the Executor container | — |

---

## Running Tests

```bash
dotnet restore
dotnet test --configuration Release
```

---

## Contributing

Contributions are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, branching conventions, coding standards, and the PR checklist.

---

## License

MIT License — Copyright (c) 2024 [Volosoft](https://volosoft.com).
See [LICENSE](LICENSE) for the full text.
