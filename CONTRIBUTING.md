# Contributing to TutorialValidator

Thank you for your interest in contributing to TutorialValidator. This project helps ensure that [ABP Framework](https://abp.io) documentation tutorials are accurate and up-to-date by automatically testing them. Every contribution — from a bug report to a new feature — directly improves the experience of developers following those tutorials.

---

## Ways to Contribute

- **Report a bug** — unexpected crashes, incorrect step extraction, false positives/negatives in validation
- **Improve tutorial coverage** — test against a new tutorial URL and report findings
- **Add a new step type** — if the Analyst cannot model a tutorial action, add a new `StepType`
- **Add a new Executor plugin** — expose a new capability to the AI agent (e.g., database queries, file diffing)
- **Add a new developer persona** — model a different experience level or behavioral constraint
- **Add a new reporter** — send results to Slack, Teams, or another notification channel
- **Improve documentation** — fix typos, clarify setup steps, add examples

---

## Reporting Bugs

Open a [GitHub issue](https://github.com/AbpFramework/TutorialValidator/issues/new) and include:

- **Operating system** and version
- **.NET SDK version** (`dotnet --version`)
- **Docker version** (`docker --version`), if using Docker mode
- **Tutorial URL** and the `--persona` used
- **Full error output** or relevant log excerpts from the `output/logs/` directory
- **`testplan.json`** if the failure is in the Executor phase (attach or paste the relevant step)

The more context you provide, the faster the issue can be diagnosed.

---

## Development Setup

**1. Fork and clone**

```bash
git clone https://github.com/AbpFramework/TutorialValidator.git
cd TutorialValidator
```

**2. Install prerequisites**

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- ABP Studio CLI: `dotnet tool install -g Volo.Abp.Studio.Cli`
- EF Core CLI: `dotnet tool install -g dotnet-ef`
- [Docker Desktop](https://www.docker.com/get-started/)

**3. Restore and build**

```bash
dotnet restore
dotnet build
```

**4. Configure environment**

```bash
cp docker/.env.example docker/.env
# Edit docker/.env and set OpenAI, OpenAI-compatible, or Azure OpenAI credentials
```

**5. Run tests**

```bash
dotnet test --configuration Release
```

All tests must pass before submitting a pull request.

---

## Branching & Pull Requests

- `main` is the stable branch. All pull requests target `main`.
- Use descriptive branch names:
  - `feature/your-feature-name` for new features
  - `fix/short-issue-description` for bug fixes
  - `docs/what-you-changed` for documentation-only changes
- Keep pull requests focused. One logical change per PR makes review faster.
- Reference the related issue in your PR description (e.g., `Closes #42`).

---

## Coding Standards

### General C# conventions

- Use **file-scoped namespaces** (`namespace Foo.Bar;`)
- Prefer **expression-bodied members** for simple properties and methods
- Use **`var`** where the type is obvious from the right-hand side
- No magic strings — use constants, enums, or configuration keys
- Follow the naming conventions already present in the file you are editing

### XML documentation comments

All public types and members must have XML doc comments:

```csharp
/// <summary>
/// Executes a single tutorial step using the AI agent.
/// </summary>
/// <param name="step">The step to execute.</param>
/// <param name="context">The current execution context.</param>
/// <returns>The result of the step execution.</returns>
public Task<StepResult> ExecuteAsync(TutorialStep step, ExecutionContext context);
```

### Semantic Kernel plugin pattern

New Executor capabilities must be implemented as Semantic Kernel plugins (see `Validator.Executor/Plugins/`). Each public method exposed to the agent requires a `[KernelFunction]` attribute and a `[Description]` attribute that explains the function's purpose in plain English — the AI agent uses this description to decide when to call it.

---

## Extending the Project

### Adding a new step type

1. Add a value to `StepType` in `Validator.Core/Models/Enums/StepType.cs`
2. Create a model class in `Validator.Core/Models/Steps/`
3. Update `StepTypeMapper` in `Validator.Executor/Execution/` to handle the new type
4. Update the Analyst prompt templates in `Validator.Analyst/Analysis/` to extract the new step type from tutorial text
5. Add unit tests in `Validator.Core.Tests/`

### Adding a new Executor plugin

1. Create a new class in `Validator.Executor/Plugins/`
2. Annotate public methods with `[KernelFunction]` and `[Description(...)]`
3. Register the plugin in `KernelFactory` in `Validator.Analyst/Analysis/` (or the Executor's kernel setup)
4. Document what scenarios the plugin is intended for

### Adding a new reporter

1. Create a new class in `Validator.Reporter/`
2. Implement the formatting logic (see `HtmlReportFormatter` or `DiscordReportFormatter` for reference)
3. Add a corresponding sender (see `EmailSender` or `DiscordSender`)
4. Add configuration keys to `appsettings.json` and document them in `README.md`
5. Wire up the new reporter in `Validator.Orchestrator/Runners/OrchestratorRunner.cs`

---

## Pull Request Checklist

Before submitting, confirm all of the following:

- [ ] `dotnet test --configuration Release` passes with no failures
- [ ] All new public types and members have XML doc comments
- [ ] No secrets, API keys, or credentials are committed
- [ ] `docker/.env` is not committed (it is gitignored — keep it that way)
- [ ] `appsettings.json` does not contain real API keys or passwords
- [ ] New configuration keys are documented in `README.md`
- [ ] The PR description explains *what* changed and *why*

---

## License Agreement

By submitting a pull request, you agree that your contribution is licensed under the [MIT License](LICENSE) and that Volosoft may distribute it as part of this project.
