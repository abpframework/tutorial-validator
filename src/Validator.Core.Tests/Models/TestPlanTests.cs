using Xunit;
using Validator.Core.Models;
using Validator.Core.Models.Steps;

namespace Validator.Core.Tests.Models;

public class TestPlanTests
{
    [Fact]
    public void TestPlan_ShouldHaveRequiredProperties()
    {
        // Arrange
        var testPlan = new TestPlan
        {
            TutorialName = "Test Tutorial",
            TutorialUrl = "https://example.com/tutorial",
            AbpVersion = "10.0",
            Configuration = new TutorialConfiguration
            {
                Ui = "mvc",
                Database = "ef",
                DbProvider = "sqlserver"
            },
            Steps = new List<TutorialStep>
            {
                new CommandStep
                {
                    StepId = 1,
                    Description = "Test step",
                    Command = "echo test",
                    Expects = new CommandExpectation
                    {
                        ExitCode = 0
                    }
                }
            }
        };

        // Act & Assert
        Assert.Equal("Test Tutorial", testPlan.TutorialName);
        Assert.Equal("https://example.com/tutorial", testPlan.TutorialUrl);
        Assert.Equal("10.0", testPlan.AbpVersion);
        Assert.NotNull(testPlan.Configuration);
        Assert.NotNull(testPlan.Steps);
        Assert.Single(testPlan.Steps);
    }

    [Fact]
    public void TutorialConfiguration_ShouldHaveRequiredProperties()
    {
        // Arrange
        var config = new TutorialConfiguration
        {
            Ui = "mvc",
            Database = "ef",
            DbProvider = "sqlserver"
        };

        // Act & Assert
        Assert.Equal("mvc", config.Ui);
        Assert.Equal("ef", config.Database);
        Assert.Equal("sqlserver", config.DbProvider);
    }

    [Fact]
    public void CommandStep_ShouldHaveRequiredProperties()
    {
        // Arrange
        var step = new CommandStep
        {
            StepId = 1,
            Description = "Test step",
            Command = "echo test",
            Expects = new CommandExpectation
            {
                ExitCode = 0,
                Creates = new List<string> { "test.txt" }
            },
            IsLongRunning = false
        };

        // Act & Assert
        Assert.Equal(1, step.StepId);
        Assert.Equal("Test step", step.Description);
        Assert.Equal("echo test", step.Command);
        Assert.NotNull(step.Expects);
        Assert.Equal(0, step.Expects.ExitCode);
        Assert.Contains("test.txt", step.Expects.Creates);
        Assert.False(step.IsLongRunning);
    }

    [Fact]
    public void CommandExpectation_ShouldHaveDefaultValues()
    {
        // Arrange
        var expectation = new CommandExpectation();

        // Act & Assert
        Assert.Equal(0, expectation.ExitCode); // Default value
        Assert.Null(expectation.Creates);
    }
}