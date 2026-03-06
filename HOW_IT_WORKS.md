# How TutorialValidator Works

This document explains what TutorialValidator does and how its pieces fit together, without going into the low-level implementation details. If you want the full technical picture, see [TECHNICAL_REFERENCE.md](TECHNICAL_REFERENCE.md).

---

## The Big Picture

TutorialValidator answers one question: **does this tutorial actually work?**

It does this by treating the tutorial as a test suite. It reads the tutorial, extracts every instruction as a structured step, and then has an AI agent carry out those steps — exactly as a real developer would — in a clean, isolated environment. If anything fails, the tutorial has a documentation bug.

The system was built to catch problems like:
- Commands that reference files not yet created at that point in the tutorial
- Code snippets with syntax errors or missing imports
- Steps that depend on a previous step that was never documented
- HTTP endpoints that are supposed to be reachable but aren't

---

## The Pipeline

Every run goes through three phases in sequence.

```
Tutorial URL
    │
    ▼
┌─────────────────────────────────────┐
│  Phase 1 — Analyst                  │
│  Scrape the tutorial pages          │
│  AI extracts structured steps       │
│  Output: testplan.json              │
└──────────────────┬──────────────────┘
                   │
                   ▼
┌─────────────────────────────────────┐
│  Phase 2 — Executor                 │
│  AI agent follows each step         │
│  Runs commands, writes files,       │
│  makes HTTP calls, checks results   │
│  Output: results/ + summary.json    │
└──────────────────┬──────────────────┘
                   │
                   ▼
┌─────────────────────────────────────┐
│  Phase 3 — Reporter                 │
│  Sends HTML email report            │
│  Posts Discord notification         │
└─────────────────────────────────────┘
```

The **Orchestrator** is the coordinator that drives all three phases, manages the Docker environment, and produces the final `summary.json`.

---

## Phase 1: The Analyst

The Analyst's job is to read the tutorial and produce a machine-readable test plan.

**Scraping** — The Analyst fetches the tutorial URL and parses the HTML. It follows navigation links within the same tutorial series, collecting up to a configurable maximum number of pages. Each page is converted to clean Markdown.

**Analysis** — The Analyst sends the Markdown content to an AI model (OpenAI or Azure OpenAI) with a prompt that instructs it to extract every action a developer must take. The result is a list of structured steps in JSON format, called the **test plan** (`testplan.json`).

**Compaction** — Long tutorials can produce hundreds of raw steps. To keep execution time and AI cost reasonable, the Analyst merges adjacent steps of the same type (e.g., two consecutive file edits become one step with two modifications). This is controlled by the `--target-steps` and `--max-steps` arguments.

The test plan is the handoff between Phase 1 and Phase 2. You can inspect it, edit it, or even write one by hand and feed it directly to the Executor.

---

## Phase 2: The Executor

The Executor is where the actual testing happens. It loads the test plan and walks through every step, using an AI agent to perform each one.

**The AI agent** is powered by [Microsoft Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/overview/) and has access to four tools:

| Tool | What it can do |
|---|---|
| Command | Execute shell commands, start background processes (e.g., web servers) |
| File Operations | Read, write, create, delete, and list files and directories |
| HTTP | Make GET and POST requests, check if a URL is reachable |
| Environment | Check installed tool versions, read environment variables, get directory info |

For each step, the agent receives a description of what needs to be done and uses these tools to carry it out. It then reports whether the step succeeded or failed.

**Fail-fast behavior** — If any step fails, the Executor stops. All remaining steps are marked as `Skipped`. This is intentional: a failed step usually means the environment is in an unexpected state, so continuing would produce misleading results.

**Long-running processes** — Some tutorial steps involve starting a web server or other background process. The Executor handles these specially: it starts the process in the background, watches its output for a readiness signal (like `Now listening on`), and keeps it running for later HTTP assertion steps.

---

## Step Types

The test plan uses four types of steps, each representing a different kind of instruction a tutorial can contain.

**Command** — A terminal command to run. Can include an expected exit code and file creation expectations. Long-running commands (like `dotnet run`) are flagged separately so the Executor knows not to wait for them to exit.

**File Operation** — Create, modify, or delete a file or directory. When creating a file, the step includes the full file content.

**Code Change** — Apply a specific code modification to an existing file. This can be a targeted find-and-replace (when you know the exact code to look for) or a full file replacement (when the tutorial shows a complete updated version of the file).

**Expectation** — Assert that something is true about the current state. There are three assertion types:
- **Build assertion** — verifies that `dotnet build` succeeds
- **HTTP assertion** — makes a request to a URL and checks the response (status code, body content)
- **Database assertion** — queries the database and checks the result

---

## Developer Personas

The `--persona` flag changes how the AI agent behaves when it runs into problems. This lets you test your tutorial at different levels of strictness.

**Junior** — Follows instructions literally. Adds `using` statements when a type is unrecognized, but makes no other assumptions. If the tutorial is ambiguous or incomplete, it fails.

**Mid** (default) — Has solid knowledge of the tech stack (C#, .NET, etc.) but no framework-specific knowledge (e.g., no knowledge of ABP internals). Understands patterns well enough to match code correctly and handle edge cases in find-and-replace operations, but won't try to fix things the tutorial doesn't address.

**Senior** — Expert in the full stack including the framework. When something fails, it diagnoses the problem, attempts a fix, and retries up to 3 times. Every fix it makes is documented in the results — these represent potential improvements to the tutorial.

The persona system exists because different questions require different answers. To find documentation gaps, use `junior` or `mid`. To validate the overall flow and check what an expert can work around, use `senior`.

---

## Output Files

After a run, the output directory contains:

| File | What it contains |
|---|---|
| `scraped/` | The raw Markdown converted from the tutorial pages |
| `testplan.json` | The structured test plan extracted from the tutorial |
| `results/validation-result.json` | Per-step pass/fail status with timing and error details |
| `results/validation-report.json` | Human-readable report with diagnostics and failure context |
| `logs/` | Full console output from the Executor |
| `summary.json` | Top-level summary: overall status, tutorial name, duration, paths to all files |

---

## Docker vs Local Mode

**Docker mode** (the default) runs the Executor inside a container. This means:
- The tutorial's commands run in a clean, reproducible environment
- Any mess created during execution stays inside the container
- The container comes pre-installed with .NET 10, Node.js 20, the ABP CLI, and the EF Core CLI
- A SQL Server instance runs as a sidecar container for tutorials that need a database

**Local mode** (`--local` flag) runs the Executor directly on your machine. This is faster to start because there's no container build step, but it means tutorial commands run against your local environment. Use this if you already have the required tools installed and want a quicker feedback loop, or if Docker is not available.

---

## Notifications

After execution, the Reporter can send notifications through two channels:

**Email** — An HTML report is formatted and sent via SMTP. The report includes the overall result, a table of all steps with their status, and detailed diagnostics for any failures.

**Discord** — A summary message is posted to a Discord webhook. Useful for team monitoring in CI/CD pipelines.

Both channels are configured in `appsettings.json` and can be disabled independently.
