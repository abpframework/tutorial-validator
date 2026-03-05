using Validator.Analyst.Analysis;
using Validator.Core;
using Validator.Core.Models;
using Validator.Core.Models.Assertions;
using Validator.Core.Models.Steps;
using Xunit;

namespace Validator.Core.Tests.Analysis;

public class StepCompactorTests
{
    [Fact]
    public void Compact_ShouldMergeAdjacentCodeChangesWithSameScope()
    {
        var steps = new List<TutorialStep>
        {
            new CodeChangeStep
            {
                StepId = 1,
                Description = "First",
                Scope = "application",
                Modifications =
                [
                    new CodeModification
                    {
                        FilePath = "src/App/BookAppService.cs",
                        SearchPattern = "class BookAppService",
                        ReplaceWith = "class BookAppService : ApplicationService"
                    }
                ]
            },
            new CodeChangeStep
            {
                StepId = 2,
                Description = "Second",
                Scope = "application",
                Modifications =
                [
                    new CodeModification
                    {
                        FilePath = "src/App/BookAppService.cs",
                        SearchPattern = "GetListAsync",
                        ReplaceWith = "GetListAsync(CancellationToken cancellationToken)"
                    }
                ]
            }
        };

        var compacted = StepCompactor.Compact(steps, new StepCompactionOptions
        {
            TargetStepCount = 1,
            MaxStepCount = 1
        });

        var codeChange = Assert.IsType<CodeChangeStep>(Assert.Single(compacted));
        Assert.Equal(2, codeChange.Modifications!.Count);
    }

    [Fact]
    public void Compact_ShouldNotMergeLongRunningCommands()
    {
        var steps = new List<TutorialStep>
        {
            new CommandStep
            {
                StepId = 1,
                Description = "Run app",
                Command = "dotnet run --project src/App.Web/App.Web.csproj",
                IsLongRunning = true,
                ReadinessPattern = "Now listening on"
            },
            new CommandStep
            {
                StepId = 2,
                Description = "Build",
                Command = "dotnet build"
            },
            new ExpectationStep
            {
                StepId = 3,
                Description = "Verify build",
                Assertions = [new BuildAssertion { Command = "dotnet build", ExpectsExitCode = 0 }]
            }
        };

        var compacted = StepCompactor.Compact(steps, new StepCompactionOptions
        {
            TargetStepCount = 2,
            MaxStepCount = 2
        });

        Assert.Equal(3, compacted.Count);
        Assert.IsType<CommandStep>(compacted[0]);
        Assert.IsType<CommandStep>(compacted[1]);
    }

    [Fact]
    public void Compact_ShouldReduceScrapedCorpusPlanCloseToTarget()
    {
        // Generate a large test plan programmatically to simulate a scraped corpus
        var steps = GenerateLargeTestPlan(stepCount: 80);

        var testPlan = new TestPlan
        {
            TutorialName = "Test Tutorial",
            TutorialUrl = "https://example.com/tutorial",
            AbpVersion = "10.0",
            Configuration = new TutorialConfiguration { Ui = "mvc", Database = "ef", DbProvider = "sqlserver" },
            Steps = steps
        };

        var compacted = StepCompactor.Compact(testPlan.Steps, new StepCompactionOptions
        {
            TargetStepCount = 50,
            MaxStepCount = 55
        });

        Assert.True(compacted.Count <= 55, $"Expected <=55 steps, got {compacted.Count}");
        Assert.True(compacted.Count < testPlan.Steps.Count, "Compaction should reduce step count.");
    }

    /// <summary>
    /// Generates a list of tutorial steps that mimics a realistic scraped tutorial corpus.
    /// Includes mergeable adjacent code changes to ensure compaction can reduce the count.
    /// </summary>
    private static List<TutorialStep> GenerateLargeTestPlan(int stepCount)
    {
        var steps = new List<TutorialStep>();
        var id = 1;

        // Start with a project creation command
        steps.Add(new CommandStep
        {
            StepId = id++,
            Description = "Create new ABP project",
            Command = "abp new TestApp -u mvc -d ef",
            Expects = new CommandExpectation { ExitCode = 0, Creates = ["TestApp"] }
        });

        // Generate groups of adjacent code changes with the same scope (mergeable)
        for (int group = 0; group < 10; group++)
        {
            var scope = group % 2 == 0 ? "application" : "domain";
            for (int i = 0; i < 5; i++)
            {
                steps.Add(new CodeChangeStep
                {
                    StepId = id++,
                    Description = $"Code change group {group} step {i}",
                    Scope = scope,
                    Modifications =
                    [
                        new CodeModification
                        {
                            FilePath = $"src/TestApp.{scope}/File{group}_{i}.cs",
                            SearchPattern = $"placeholder_{group}_{i}",
                            ReplaceWith = $"replacement_{group}_{i}"
                        }
                    ]
                });
            }

            // Insert a build command between groups to break merge sequences
            if (group % 3 == 2)
            {
                steps.Add(new CommandStep
                {
                    StepId = id++,
                    Description = $"Build after group {group}",
                    Command = "dotnet build",
                    Expects = new CommandExpectation { ExitCode = 0 }
                });
            }
        }

        // Pad remaining steps if needed
        while (steps.Count < stepCount)
        {
            steps.Add(new CommandStep
            {
                StepId = id++,
                Description = $"Additional build step {steps.Count}",
                Command = "dotnet build",
                Expects = new CommandExpectation { ExitCode = 0 }
            });
        }

        return steps;
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TutorialValidator.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

