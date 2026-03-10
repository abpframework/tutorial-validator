# Technical Reference

This document is the deep-dive companion to [HOW_IT_WORKS.md](HOW_IT_WORKS.md). It covers architecture, internal pipelines, schemas, the plugin system, CI/CD integration, and how to extend the tool.

---

## Table of Contents

1. [Project Architecture](#1-project-architecture)
2. [The Analyst Pipeline In Depth](#2-the-analyst-pipeline-in-depth)
3. [The Executor Pipeline In Depth](#3-the-executor-pipeline-in-depth)
4. [The Persona System](#4-the-persona-system)
5. [testplan.json Schema](#5-testplanjson-schema)
6. [Docker Execution Mode](#6-docker-execution-mode)
7. [AI Provider Configuration](#7-ai-provider-configuration)
8. [Notification System](#8-notification-system)
9. [CI/CD Integration](#9-cicd-integration)
10. [Extending the Tool](#10-extending-the-tool)
11. [Known Discrepancies and Notes](#11-known-discrepancies-and-notes)

---

## 1. Project Architecture

The solution is split into five projects, each with a distinct responsibility.

```
TutorialValidator.sln
├── Validator.Core          Shared models, enums, result types, JSON serialization
├── Validator.Analyst       Scrapes tutorial HTML; AI extracts testplan.json
├── Validator.Executor      AI agent executes a testplan.json step by step
├── Validator.Orchestrator  Top-level CLI; coordinates all phases; Docker lifecycle
└── Validator.Reporter      Formats and sends email/Discord notifications
```

### Validator.Core

Contains all types shared between projects. Nothing in `Core` references any other project in the solution.

Key namespaces:
- `Validator.Core.Models` — `TestPlan`, `TutorialStep` (polymorphic base), `TutorialConfiguration`
- `Validator.Core.Models.Steps` — `CommandStep`, `FileOperationStep`, `CodeChangeStep`, `ExpectationStep`, `CodeModification`, `CommandExpectation`
- `Validator.Core.Models.Assertions` — `Assertion` (polymorphic base), `BuildAssertion`, `HttpAssertion`, `DatabaseAssertion`
- `Validator.Core.Models.Results` — `ValidationResult`, `ValidationReport`, `StepResult`, `FailureDiagnostic`
- `Validator.Core.Models.Enums` — `StepType`, `StepExecutionStatus`, `ValidationStatus`, `FileOperationType`, `FailureClassification`

`TutorialStep` uses `System.Text.Json` polymorphic serialization with a `"type"` discriminator property. `Assertion` uses a `"kind"` discriminator. This means `testplan.json` can be deserialized into the correct concrete type without custom converters.

### Validator.Analyst

A standalone CLI (`dotnet run --project src/Validator.Analyst -- <command>`) with three commands:
- `scrape` — fetch and convert HTML to Markdown
- `analyze` — send Markdown to AI and produce `testplan.json`
- `full` — run both in sequence

### Validator.Executor

A standalone CLI (`dotnet run --project src/Validator.Executor -- run`) that loads a `testplan.json`, initializes a Semantic Kernel with four plugins, and has an AI agent execute each step.

### Validator.Orchestrator

The top-level CLI used by end users. Commands:
- `run` — full pipeline (Analyst + Executor + Reporter)
- `docker-only` — Executor in Docker with an existing test plan
- `analyst-only` — Analyst only (scrape + analyze)

The Orchestrator invokes Analyst and Executor as child processes via `ProcessHelper`, which allows them to run in separate process spaces (important for Docker mode, where the Executor runs inside a container).

### Validator.Reporter

Library (no CLI) referenced by Orchestrator. Contains:
- `EmailReportNotifier` — formats `ValidationReport` as HTML and sends via SMTP
- `DiscordReportNotifier` — formats `ValidationReport` as a Discord message and posts to a webhook

---

## 2. The Analyst Pipeline In Depth

### 2.1 Scraping

Entry point: `TutorialScraper.ScrapeAsync(url, maxPages)`

1. `HttpFetcher` downloads the page HTML using `HttpClient`.
2. `HtmlParser` (backed by [AngleSharp](https://anglesharp.github.io/)) parses the HTML.
3. `ContentExtractor` strips navigation, headers, footers, and other chrome, leaving only the tutorial body.
4. `MarkdownCleaner` converts the extracted HTML to clean Markdown.
5. `NavigationExtractor` scans the page for links that belong to the same tutorial series (same URL prefix, same nav structure) and queues them for scraping.
6. Steps 1–5 repeat for each discovered page, up to the `--max-pages` limit.

Output: a `ScrapedTutorial` object containing the title, source URL, and an ordered list of pages with their Markdown content. This is also written to `output/scraped/`.

### 2.2 Configuration Detection

Before calling the AI, `ConfigurationDetector` scans the Markdown for known patterns to detect:
- **UI framework** — looks for strings like `mvc`, `blazor`, `angular`, `no-ui`
- **Database** — looks for `ef`, `mongodb`
- **DB provider** — looks for `sqlserver`, `mysql`, `postgresql`, `sqlite`, `oracle`
- **ABP version** — extracts version numbers from package references or CLI commands
- **Tutorial name** — extracted from the page title

This metadata is embedded in the test plan's `TutorialConfiguration` and used to build the correct `abp new` command if one is missing.

### 2.3 Step Extraction

`StepExtractor.ExtractAllStepsAsync` sends each page's Markdown to the AI model using `Microsoft.SemanticKernel`. The prompt is defined in `SystemPrompts.cs` and `UserPrompts.cs`.

The AI returns a JSON array of step objects. The extractor deserializes these into `TutorialStep` instances. Each page is processed independently and the results are concatenated.

### 2.4 Normalization

`StepNormalizer.ValidateAndNormalize` runs a series of cleanup rules on the raw extracted steps:

- Removes duplicate steps (by command or file path)
- Removes steps with empty or missing required fields
- Normalizes file paths to use forward slashes
- Infers missing step types from field presence

### 2.5 Project Creation Inference

`ProjectNameInferrer.NeedsProjectCreationStep` checks whether the step list contains an `abp new` command. If not, `ProjectNameInferrer.InferProjectNameFromSteps` scans file paths in the steps for a common root namespace (e.g., `Acme.BookStore`) and constructs the project name from it.

If a project name is inferred, a `CommandStep` with the `abp new <Name> -u <ui> -d <db> -o <Name>` command is prepended to the list.

### 2.6 Compaction

Long tutorials may produce 100+ raw steps. The `StepCompactor` reduces this to a manageable number while preserving correctness.

**Phase 1 compaction** (runs when `steps.Count > TargetStepCount`, default 50):
1. `MergeAdjacentCodeChanges` — merges consecutive `CodeChangeStep` entries with the same `Scope` (e.g., two consecutive domain-layer code changes become one step with multiple `Modifications`). Respects `MaxCodeModificationsPerStep` (default: not set explicitly, derived from compaction loop).
2. `MergeAdjacentExpectations` — merges consecutive `ExpectationStep` entries into one step with multiple assertions.
3. `MergeAdjacentCommands` — merges consecutive `CommandStep` entries using `&&` chaining. Long-running commands and project creation commands are never merged.
4. `RemoveRedundantBuildExpectations` — when multiple consecutive build expectations appear, only the last one is kept.

**Phase 2 compaction** (runs when `steps.Count > MaxStepCount`, default 55):
5. `RemoveDirectoryCreateBeforeFileCreate` — if a `CreateDirectory` step is immediately followed by a `CreateFile` inside that directory, the directory step is removed (the write tool creates parent directories automatically).
6. `ConvertFileOperationsToCodeChanges` — `FileOperationStep` entries with `Create` or `Modify` operation and non-empty content are converted to `CodeChangeStep` entries, allowing them to participate in code change merging.
7. Repeat `MergeAdjacentCodeChanges` and `MergeAdjacentExpectations` with higher limits.
8. `MergeAdjacentCodeChangesAnyScope` — same as `MergeAdjacentCodeChanges` but ignores scope boundaries, allowing cross-scope merges as a last resort.

After compaction, all steps are renumbered sequentially starting from 1.

---

## 3. The Executor Pipeline In Depth

### 3.1 Initialization

`AgentKernelFactory.CreateExecutorKernel` builds a `Microsoft.SemanticKernel.Kernel` with:
- The configured AI chat completion service (OpenAI, Azure OpenAI, or OpenAI-compatible endpoint)
- Four plugins registered as kernel functions (see [3.3 Plugins](#33-plugins))
- A `FunctionCallTracker` that intercepts and records every function call and its result for deterministic result parsing

### 3.2 Execution Loop

`ExecutionOrchestrator.ExecuteAsync` iterates through all steps in `StepId` order:

1. Before each step, `InjectBackgroundBaseUrl` checks whether a background process is running and, if so, rewrites any placeholder ports (`<port>`) or relative paths in HTTP assertion URLs to use the actual listening address captured from the process output.

2. `StepExecutionAgent.ExecuteStepAsync` is called for the current step. This is the main AI interaction point (see [3.4 Agent Loop](#34-agent-loop)).

3. If the step result is `Failed`, execution stops. All remaining steps are added to the result with status `Skipped`.

4. After HTTP assertion steps, `StopBackgroundProcessIfDone` checks whether the next step also contains HTTP assertions. If not, the background process is stopped (the web server is no longer needed).

5. In the `finally` block, any still-running background process is always stopped.

`StepPreprocessor.NormalizeStepsForExecution` runs before the loop and applies runtime-only fixes, such as ensuring `abp new` commands include an `-o <name>` flag so the project is created in a subdirectory rather than the current directory.

### 3.3 Plugins

The four Semantic Kernel plugins expose functions that the AI agent can call during step execution.

#### CommandPlugin (`Validator.Executor.Plugins.CommandPlugin`)

| Function | Description |
|---|---|
| `ExecuteCommandAsync(command, workingDirectory?)` | Runs a command via `cmd.exe` (Windows) or `/bin/bash` (Linux/macOS). Captures stdout, stderr, and exit code. Returns a formatted string. Output is truncated to the last 6,000 characters if it exceeds that limit. Default timeout: 5 minutes. |
| `StartBackgroundProcessAsync(command, workingDirectory?, readinessPattern, timeoutSeconds)` | Starts a command in the background without waiting for it to exit. Monitors stdout for a line matching `readinessPattern` (a regex). Captures the URL from the matching line (e.g., `https://localhost:44312` from `Now listening on https://localhost:44312`) and stores it in `BackgroundProcessBaseUrl`. Returns success once the readiness signal is detected, or failure if the timeout elapses. Only one background process can be active at a time; starting a new one stops the previous one. |

The `StopBackgroundProcess()` method (not a kernel function — called directly by the orchestrator) kills the background process and clears the stored URL.

#### FileOperationsPlugin (`Validator.Executor.Plugins.FileOperationsPlugin`)

| Function | Description |
|---|---|
| `ReadFileAsync(path)` | Reads and returns file contents as a string. |
| `WriteFileAsync(path, content)` | Writes content to a file. Creates parent directories automatically. |
| `DeleteAsync(path)` | Deletes a file or directory (recursive for directories). |
| `ListDirectoryAsync(path, recursive?)` | Lists directory contents. |
| `ExistsAsync(path)` | Returns whether a path exists and whether it is a file or directory. |
| `CreateDirectoryAsync(path)` | Creates a directory (and all parent directories). |

#### HttpPlugin (`Validator.Executor.Plugins.HttpPlugin`)

| Function | Description |
|---|---|
| `GetAsync(url, headers?)` | Sends an HTTP GET request and returns the status code and response body. |
| `PostAsync(url, body?, headers?)` | Sends an HTTP POST request. |
| `RequestAsync(method, url, body?, headers?)` | Generic HTTP request for any method. |
| `IsReachableAsync(url, timeoutSeconds?)` | Returns whether the URL responds to a GET within the timeout. |

#### EnvironmentPlugin (`Validator.Executor.Plugins.EnvironmentPlugin`)

| Function | Description |
|---|---|
| `GetEnvironmentInfoAsync()` | Returns OS, .NET version, and available memory. |
| `GetToolVersionAsync(tool)` | Runs `<tool> --version` and returns the output. |
| `GetCurrentDirectoryAsync()` | Returns the current working directory. |
| `GetEnvironmentVariableAsync(name)` | Returns the value of an environment variable. |

### 3.4 Agent Loop

`StepExecutionAgent.ExecuteStepAsync` is the core interaction:

1. A persona-specific system prompt is set (see [Section 4](#4-the-persona-system)).
2. A step-type-specific user prompt is built by `ExecutorPrompts.ForCommandStep / ForFileOperationStep / ForCodeChangeStep / ForExpectationStep`. These prompts include the step description, the specific instructions (command, file path, modifications, assertions), and persona-specific behavioral rules.
3. The prompt is sent to the chat completion service with `FunctionChoiceBehavior.Auto()`, which allows the model to call any registered plugin function.
4. The model executes the step by calling plugin functions and returns a natural-language summary of what it did and whether it succeeded.
5. `AgentResponseParser.ParseResponse` interprets the response. When a `FunctionCallTracker` is available, it uses the actual function call results (exit codes, HTTP status codes) for deterministic pass/fail determination rather than relying on the model's text. If no tracker is available (dry run), it falls back to text pattern analysis.

**Senior retry loop:** For the `Senior` persona, `ExecuteStepAsync` wraps the above in a loop of up to `SeniorMaxRetries + 1 = 4` total attempts. On each failure, `BuildRetryContext` appends the error details to the next prompt, giving the model context for its fix attempt. If `EXECUTOR_BUILD_GATE_INTERVAL` is set (e.g., `5`), a `dotnet build` is run after every N code-modifying steps; a build failure feeds into the same retry loop.

---

## 4. The Persona System

The persona is passed to `StepExecutionAgent` and controls two things: the system prompt content and the retry behavior.

### System Prompts

All three system prompts are defined as compile-time constants in `ExecutorPrompts.cs` and share several reusable fragments:

- **`WorkspaceStructure`** — instructs the agent to always list the workspace directory before operating on files, because the project created by `abp new` lives in a subdirectory whose name is not known in advance.
- **`AvailableTools`** — brief description of all four plugins.
- **`ResponseFormat`** — format for the agent's final response.
- **`UsingStatementRule`** — applied to all personas. Instructs the agent to add missing `using` directives after writing C# files, because modern IDEs do this automatically and its absence would cause false failures.
- **`SeniorSideEffectRules`** — applied to Senior only. Five strict rules to prevent side effects: no stub files, no duplicate migrations, minimal fixes, no re-running idempotent commands, clean up after failed fix attempts.

### Persona Behavioral Differences

| Aspect | Junior | Mid | Senior |
|---|---|---|---|
| System prompt | Literal execution, no problem-solving | Literal execution, C# syntax awareness | Expert problem-solving, full ABP knowledge |
| On command failure | Report and stop | Report and stop | Analyze, fix, retry |
| On code change failure | Report and stop | Report and stop; semantic pattern matching for find/replace | Report and stop; full semantic matching + ABP knowledge |
| On file create: existing file check | Overwrite | Smart merge (search for existing file by name, merge content into namespace) | Smart merge (same as Mid) |
| Build gate | No | No | Optional (`EXECUTOR_BUILD_GATE_INTERVAL`) |
| Retry count | 0 | 0 | Up to 3 |
| Fix reporting | N/A | N/A | Required — reports what was changed and why |

---

## 5. testplan.json Schema

The test plan is the contract between the Analyst and the Executor. It is a JSON file conforming to the following structure.

### Root Object

```json
{
  "tutorialName": "string",
  "tutorialUrl": "string",
  "abpVersion": "string",
  "configuration": { ... },
  "steps": [ ... ]
}
```

| Field | Type | Description |
|---|---|---|
| `tutorialName` | string | Human-readable name of the tutorial. |
| `tutorialUrl` | string | Source URL of the tutorial. |
| `abpVersion` | string | Detected ABP Framework version (e.g., `"9.0.0"`). |
| `configuration` | object | Detected tutorial configuration (see below). |
| `steps` | array | Ordered list of steps. |

### TutorialConfiguration Object

```json
{
  "ui": "mvc",
  "database": "ef",
  "dbProvider": "sqlserver"
}
```

| Field | Type | Description |
|---|---|---|
| `ui` | string | UI framework: `mvc`, `blazor`, `angular`, `no-ui`. |
| `database` | string | Database type: `ef`, `mongodb`. |
| `dbProvider` | string | DB provider (EF only): `sqlserver`, `mysql`, `postgresql`, `sqlite`, `oracle`. |

### Step Types

All steps share a `"type"` discriminator, a `"stepId"` integer, and an optional `"description"` string.

#### CommandStep (`"type": "command"`)

```json
{
  "type": "command",
  "stepId": 1,
  "description": "Create the ABP solution",
  "command": "abp new Acme.BookStore -u mvc -d ef -o Acme.BookStore",
  "expects": {
    "exitCode": 0,
    "creates": ["Acme.BookStore"]
  },
  "isLongRunning": false,
  "readinessPattern": null,
  "readinessTimeoutSeconds": 60
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `command` | string | Yes | The exact shell command to run. |
| `expects` | object | No | Expected outcomes of the command. |
| `expects.exitCode` | int | No | Expected exit code (default 0). |
| `expects.creates` | string[] | No | Paths or directory names expected to exist after the command. |
| `isLongRunning` | bool | No | `true` for web servers or watchers. Uses `StartBackgroundProcessAsync` instead of `ExecuteCommandAsync`. |
| `readinessPattern` | string | No | Regex pattern to match in stdout when `isLongRunning` is `true`. Default: `"Now listening on"`. |
| `readinessTimeoutSeconds` | int | No | Seconds to wait for the readiness pattern. Default: `60`. |

#### FileOperationStep (`"type": "file_operation"`)

```json
{
  "type": "file_operation",
  "stepId": 5,
  "description": "Create appsettings.json",
  "operation": "Create",
  "path": "src/Acme.BookStore.Web/appsettings.json",
  "entityType": "file",
  "content": "{ ... }"
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `operation` | string | Yes | `Create`, `Modify`, or `Delete`. |
| `path` | string | Yes | Path relative to the project root. |
| `entityType` | string | Yes | `"file"` or `"directory"`. |
| `content` | string | No | Content to write for `Create` and `Modify` operations on files. |

#### CodeChangeStep (`"type": "code_change"`)

```json
{
  "type": "code_change",
  "stepId": 12,
  "description": "Add Book entity",
  "scope": "domain",
  "expectedFiles": ["src/Acme.BookStore.Domain/Books/Book.cs"],
  "modifications": [
    {
      "filePath": "src/Acme.BookStore.Domain/Books/Book.cs",
      "fullContent": "using System;\nnamespace Acme.BookStore.Books;\n..."
    },
    {
      "filePath": "src/Acme.BookStore.Domain/BookStoreDbProperties.cs",
      "searchPattern": "public const string DbTablePrefix",
      "replaceWith": "public const string DbTablePrefix = \"Books\";"
    }
  ]
}
```

| Field | Type | Required | Description |
|---|---|---|---|
| `scope` | string | Yes | Architectural layer of the change (e.g., `domain`, `application`, `infrastructure`, `web`). Used by compaction to determine which adjacent steps can be merged. |
| `expectedFiles` | string[] | No | Paths of files expected to exist or be created by this step. |
| `modifications` | array | No | List of `CodeModification` objects. |

**CodeModification object:**

| Field | Type | Required | Description |
|---|---|---|---|
| `filePath` | string | Yes | Path relative to the project root. |
| `fullContent` | string | No | Full file content. When present, the file is created or overwritten. Takes precedence over `searchPattern`/`replaceWith`. |
| `searchPattern` | string | No | Exact text to find in the file. |
| `replaceWith` | string | No | Replacement text. Used together with `searchPattern`. |

At least one of `fullContent` or `searchPattern`+`replaceWith` must be provided per modification.

#### ExpectationStep (`"type": "expectation"`)

```json
{
  "type": "expectation",
  "stepId": 20,
  "description": "Verify application is running",
  "assertions": [
    {
      "kind": "build",
      "command": "dotnet build",
      "expectsExitCode": 0
    },
    {
      "kind": "http",
      "url": "https://localhost:<port>/api/app/book",
      "method": "GET",
      "expectsStatus": 200,
      "expectsContent": "items"
    }
  ]
}
```

**Assertion types:**

`BuildAssertion` (`"kind": "build"`):

| Field | Type | Default | Description |
|---|---|---|---|
| `command` | string | `"dotnet build"` | Build command to run. |
| `expectsExitCode` | int | `0` | Expected exit code. |

`HttpAssertion` (`"kind": "http"`):

| Field | Type | Default | Description |
|---|---|---|---|
| `url` | string | — | URL to request. May contain `<port>` as a placeholder, which is replaced at runtime with the actual port of the running background process. |
| `method` | string | `"GET"` | HTTP method. |
| `expectsStatus` | int | `200` | Expected HTTP status code. |
| `expectsContent` | string | null | If set, the response body must contain this string. |

`DatabaseAssertion` (`"kind": "database"`):

| Field | Type | Required | Description |
|---|---|---|---|
| `provider` | string | Yes | `"ef"` or `"mongodb"`. |
| `expects` | object | Yes | `DatabaseExpectation` object. |
| `expects.migrationsApplied` | bool | No | Whether all EF Core migrations should be applied. |
| `expects.tablesExist` | string[] | No | Database table names expected to exist. |

---

## 6. Docker Execution Mode

### Container Topology

When the Orchestrator runs in Docker mode (the default), `docker-compose.yml` starts two containers on a shared `validator-network` bridge network.

**`sqlserver` container:**
- Image: `mcr.microsoft.com/mssql/server:2022-latest`
- Port `1433` forwarded to the host
- Health check: runs `sqlcmd -Q "SELECT 1"` every 10 seconds with a 5-second timeout, up to 10 retries, after a 30-second startup grace period
- The `executor` container waits for this health check to pass before starting (`depends_on: sqlserver: condition: service_healthy`)

**`executor` container:**
- Built from `docker/Dockerfile`
- Mounts `output/` as `/output` (read/write) — `testplan.json` flows in, results flow out
- Mounts `executor-workspace` Docker volume as `/workspace` — ephemeral working space for generated projects
- All AI credentials and configuration are passed as environment variables from `docker/.env`
- Default entrypoint: `dotnet /app/publish/Validator.Executor.dll`
- Default command: `run --input /output/testplan.json --workdir /workspace --output /output/results`

### Executor Docker Image

Built from `docker/Dockerfile` using `mcr.microsoft.com/dotnet/sdk:10.0` as the base.

Additional tooling installed during image build:
- **Node.js 20** — required for ABP Angular/Blazor frontend scaffolding
- **ABP CLI** (`Volo.Abp.Studio.Cli`) — installed as a global .NET tool
- **EF Core CLI** (`dotnet-ef`) — installed as a global .NET tool

A **pre-warmup step** runs `abp new WarmupProject -u mvc ...` and then deletes the output. This forces the ABP CLI to download its extensions (`StandardSolutionTemplates` and others) and cache them in the image layer. Without this, the first `abp new` command in a real run triggers an extension download and CLI restart, which confuses the AI agent's output parser.

A workaround for the .NET 10 SDK image ships an RC/preview runtime while ABP CLI and EF tools target the stable `10.0.0` runtime: the Dockerfile installs both the `dotnet` and `aspnetcore` 10.0.0 stable runtimes side by side using `dotnet-install.sh`. This block can be removed once `mcr.microsoft.com/dotnet/sdk:10.0` ships with the stable runtime.

### Volume Details

| Volume | Type | Mount in container | Purpose |
|---|---|---|---|
| `${OUTPUT_PATH:-../output}` | bind mount | `/output` | Shared I/O between host and container |
| `executor-workspace` | named volume | `/workspace` | Ephemeral workspace for generated projects |
| `sqlserver-data` | named volume | `/var/opt/mssql` | SQL Server data files |

---

## 7. AI Provider Configuration

### Provider Auto-Detection

`AgentKernelFactory.LoadConfiguration` reads `appsettings.json` and then overlays environment variables (using standard `IConfiguration` chaining). Provider selection follows this logic:

1. If `AI_PROVIDER` is set explicitly, use that value (`OpenAI` or `AzureOpenAI`).
2. If `AZURE_OPENAI_ENDPOINT` is set, default to `AzureOpenAI`.
3. Otherwise, if `OPENAI_COMPAT_BASE_URL` and `OPENAI_COMPAT_API_KEY` are present, use `OpenAICompatible`.
4. Otherwise, use `OpenAI`.

### OpenAI Configuration

Required: `OPENAI_API_KEY` or `AI.ApiKey` in `appsettings.json`.

Optional: `OPENAI_MODEL` or `AI.Model` (controls the model name in API requests).

The `AI.DeploymentName` field is not used for OpenAI — only `AI.Model` matters.

### Azure OpenAI Configuration

Required:
- `AZURE_OPENAI_ENDPOINT` or `AI.ApiKey` equivalent — the Azure resource endpoint URL
- `AZURE_OPENAI_API_KEY` — the API key
- `AZURE_OPENAI_DEPLOYMENT` or `AI.DeploymentName` — the deployment name in your Azure resource

`AI.Model` is ignored for Azure OpenAI; the model is determined by the deployment.

### OpenAI-Compatible Configuration

Required:
- `OPENAI_COMPAT_BASE_URL` or `AI.BaseUrl` in `appsettings.json`
- `OPENAI_COMPAT_API_KEY` or `AI.ApiKey`
- `OPENAI_COMPAT_MODEL` or `AI.ModelId` (falls back to `AI.DeploymentName`)

Optional:
- `OPENAI_COMPAT_ORG` / `AI.Organization`
- `OPENAI_COMPAT_PROJECT` / `AI.Project`

Set `AI_PROVIDER=OpenAICompatible` to force this mode when multiple provider variables are present.

### Configuration Precedence

Environment variables always override `appsettings.json`. The configuration is loaded using `Microsoft.Extensions.Configuration.IConfigurationBuilder`:

```
appsettings.json  →  environment variables
```

The `docker/.env` file is loaded by Docker Compose and passed as environment variables to the executor container, which is why API keys set in `.env` override the default values in `appsettings.json`.

---

## 8. Notification System

### Email

`EmailReportNotifier` sends an HTML-formatted report after each run when `Email.Enabled` is `true`.

`HtmlReportFormatter.Format(ValidationReport)` generates a self-contained HTML document containing:
- Overall pass/fail status with a color indicator
- Tutorial name, URL, execution duration
- A table of all steps with status icons, descriptions, and timing
- Expanded error detail for failed steps, including `ErrorMessage` and `ErrorOutput`

The email is sent via the `EmailSender` class using `System.Net.Mail.SmtpClient`. SSL/TLS is configured via `Email.UseSsl`. Authentication is optional — leave `Username` and `Password` empty for unauthenticated SMTP.

### Discord

`DiscordReportNotifier` posts a notification when `Discord.Enabled` is `true` and `Discord.WebhookUrl` is non-empty.

`DiscordReportFormatter.Format(ValidationReport)` produces a `DiscordMessage` object with:
- An embed with the overall result as the embed color (green = passed, red = failed)
- Fields for tutorial name, total steps, passed steps, failed steps, duration
- A truncated failure summary if any steps failed

`DiscordSender` posts the message as JSON to the webhook URL using `HttpClient`.

---

## 9. CI/CD Integration

### GitHub Actions

#### `.github/workflows/ci.yml`

Triggers:
- Push or pull request to `main` — builds and runs unit tests
- Push to `main` or `workflow_dispatch` — full tutorial validation run

The validation job:
1. Builds the solution
2. Runs `Validator.Core.Tests`
3. Runs the Orchestrator against the configured tutorial URL
4. Uploads the `output/` directory as a GitHub Actions artifact
5. On PR, posts a comment with the validation summary

Secrets expected: `OPENAI_API_KEY` (or Azure equivalent).

#### `.github/workflows/docker.yml`

Triggers: version tags (`v*`) or push to `main`.

Builds the Executor Docker image and pushes it to a configured registry. The registry and image name are configured via repository variables/secrets.

### Jenkins

`Jenkinsfile` defines a parameterized pipeline with the following parameters:

| Parameter | Default | Description |
|---|---|---|
| `TUTORIAL_URL` | — | Tutorial URL to validate |
| `PERSONA` | `mid` | Developer persona |
| `ENVIRONMENT` | `production` | Target environment label |
| `KEEP_CONTAINERS` | `false` | Keep containers after the run |

Pipeline stages: Build → Test → Validate → Report → Deploy (conditional on tag).

---

## 10. Extending the Tool

### Adding a New Step Type

1. **Define the model** in `Validator.Core`. Create a new class inheriting from `TutorialStep`:
   ```csharp
   public class MyNewStep : TutorialStep
   {
       public required string SomeField { get; set; }
   }
   ```

2. **Register the JSON discriminator** on `TutorialStep` in `Validator.Core/Models/TutorialStep.cs`:
   ```csharp
   [JsonDerivedType(typeof(MyNewStep), "my_new_type")]
   ```

3. **Add a `StepType` enum value** in `Validator.Core/Models/Enums/StepType.cs`.

4. **Map the type** in `Validator.Executor/Execution/StepTypeMapper.cs`.

5. **Add an Executor prompt** in `ExecutorPrompts.cs` — a `ForMyNewStep(...)` method for each persona.

6. **Handle the step in `StepExecutionAgent.BuildUserPrompt`** — add a `case MyNewStep:` branch.

7. **Teach the Analyst** — add extraction logic in the Analyst's AI prompt templates (`SystemPrompts.cs` / `UserPrompts.cs`) to recognize and extract your new step type.

8. **Add compaction rules** in `StepCompactor.cs` if the new step type can be merged.

### Adding a New Semantic Kernel Plugin

1. Create a class in `Validator.Executor/Plugins/`:
   ```csharp
   public class MyPlugin
   {
       [KernelFunction]
       [Description("Description the AI will see")]
       public async Task<string> MyFunctionAsync(
           [Description("Parameter description")] string param)
       {
           // implementation
       }
   }
   ```

2. Register the plugin in `AgentKernelFactory.CreateExecutorKernel`:
   ```csharp
   kernel.Plugins.AddFromObject(new MyPlugin(), "MyPlugin");
   ```

3. Describe the plugin in the `AvailableTools` constant in `ExecutorPrompts.cs` so the AI knows it exists.

### Adding a New Reporter

1. Implement `IReportNotifier` in `Validator.Reporter`:
   ```csharp
   public class SlackReportNotifier : IReportNotifier
   {
       public async Task SendAsync(ValidationReport report) { ... }
   }
   ```

2. Add configuration fields to `appsettings.json` and a corresponding configuration class.

3. Instantiate and invoke the notifier in `OrchestratorRunner` in `Validator.Orchestrator/Runners/OrchestratorRunner.cs`, alongside the existing email and Discord notifiers.

---

## 11. Known Discrepancies and Notes

### gpt-5.2 in appsettings.json

The `src/Validator.Orchestrator/appsettings.json` currently uses `gpt-5.2` as the model. This was the model in use when this file was last committed. The `docker/.env.example` references `gpt-4o`. Set `AI.Model` (or `OPENAI_MODEL`) to whatever model you want to use — the field accepts any valid model name. There is no hard-coded default enforced at runtime.

### Timeout Default Mismatch

The `OrchestratorOptions` class sets a `TimeoutMinutes` default of `120` in its code, but `appsettings.json` sets `Orchestrator.TimeoutMinutes` to `60`. In practice, when running via `dotnet run`, the `appsettings.json` value takes precedence, so the effective default is `60` minutes. The `--timeout` CLI flag overrides both.

### DeploymentName vs Model

`appsettings.json` has both `AI.Model` and `AI.DeploymentName`. For OpenAI, only `Model` is used. For Azure OpenAI, only `DeploymentName` is used. Keeping them in sync (same value) avoids confusion when switching providers.

### SQL Server Sidecar

The default Docker Compose configuration includes a SQL Server sidecar. This is specific to the ABP Framework tutorial validation use case. If you are validating tutorials that do not require SQL Server, you can remove the `sqlserver` service and the `depends_on` condition from `docker-compose.yml`.

### Empty Issue Templates

The `.github/ISSUE_TEMPLATE/` directory exists but is empty. GitHub issue templates have not yet been defined.
