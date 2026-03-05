using Validator.Core.Models;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Results;
using Validator.Reporter;
using Validator.Reporter.Discord;
using Validator.Reporter.Formatting;
using Xunit;

namespace Validator.Core.Tests.Reporter;

public class DiscordReportFormatterTests
{
    [Fact]
    public void FormatMessages_ShouldSplitLongStepResults_WithoutTruncationMarker()
    {
        var report = CreateReport(140);
        var formatter = new DiscordReportFormatter();

        var messages = formatter.FormatMessages(report);
        var stepMessages = messages
            .Where(m => m.Embeds.Count > 0 && m.Embeds[0].Fields.Any(f => f.Name == "Step Results"))
            .ToList();

        Assert.True(stepMessages.Count > 1);

        var allStepText = string.Join(
            '\n',
            stepMessages.Select(m => m.Embeds[0].Fields.First(f => f.Name == "Step Results").Value));

        Assert.Contains("Step 1", allStepText);
        Assert.Contains("Step 140", allStepText);
        Assert.DoesNotContain("… (truncated)", allStepText);
    }

    [Fact]
    public async Task SendReportAsync_ShouldSendAllFormattedMessages_InOrder()
    {
        var report = CreateReport(120);
        var formatter = new DiscordReportFormatter();
        var expected = formatter.FormatMessages(report);
        var sender = new RecordingDiscordSender();
        var notifier = new DiscordReportNotifier(
            sender,
            formatter,
            new DiscordConfiguration { Enabled = true, WebhookUrl = "https://example.test/webhook" });

        await notifier.SendReportAsync(report);

        Assert.Equal(expected.Count, sender.Messages.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Embeds[0].Title, sender.Messages[i].Embeds[0].Title);
        }
    }

    [Fact]
    public void FormatNotificationMessage_ShouldReturnFailedStepMessage()
    {
        var report = CreateReport(120);
        report.Result.Status = ValidationStatus.Failed;
        report.Result.PassedSteps = 95;
        report.Result.FailedSteps = 1;
        report.Result.FailedAtStepId = 96;
        report.Result.StepResults[95].Status = StepExecutionStatus.Failed;
        report.Result.StepResults[95].ErrorMessage = "dotnet build failed";

        var formatter = new DiscordReportFormatter();
        var message = formatter.FormatNotificationMessage(report);

        Assert.Equal("Error on step 96/120: dotnet build failed", message.Content);
    }

    [Fact]
    public void FormatNotificationMessage_ShouldFallbackToDetailsWhenErrorMessageMissing()
    {
        var report = CreateReport(120);
        report.Result.Status = ValidationStatus.Failed;
        report.Result.PassedSteps = 95;
        report.Result.FailedSteps = 1;
        report.Result.FailedAtStepId = 96;
        report.Result.StepResults[95].Status = StepExecutionStatus.Failed;
        report.Result.StepResults[95].Details = "command exited with code 1";

        var formatter = new DiscordReportFormatter();
        var message = formatter.FormatNotificationMessage(report);

        Assert.Equal("Error on step 96/120: command exited with code 1", message.Content);
    }

    [Fact]
    public void FormatMessages_ShouldIncludeFailedStepInSummary_WhenFailed()
    {
        var report = CreateReport(120);
        report.Result.Status = ValidationStatus.Failed;
        report.Result.PassedSteps = 95;
        report.Result.FailedSteps = 1;
        report.Result.FailedAtStepId = 96;
        report.Result.StepResults[95].Status = StepExecutionStatus.Failed;
        report.Result.StepResults[95].ErrorMessage = "dotnet build failed";

        var formatter = new DiscordReportFormatter();
        var messages = formatter.FormatMessages(report);
        var summary = messages[0].Embeds[0];
        var failedStepField = summary.Fields.FirstOrDefault(f => f.Name == "Failed Step");

        Assert.NotNull(failedStepField);
        Assert.Equal("Error on step 96/120: dotnet build failed", failedStepField!.Value);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SendReportAsync_ShouldPost100Steps_ToRealDiscordWebhook()
    {
        var webhookUrl = GetDiscordWebhookUrl();
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        var report = CreateReport(100);
        var notifier = new DiscordReportNotifier(
            new DiscordConfiguration
            {
                Enabled = true,
                WebhookUrl = webhookUrl!
            });

        await notifier.SendReportAsync(report);
    }

    private static string? GetDiscordWebhookUrl()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Tests.json");
        if (!File.Exists(configPath))
        {
            return null;
        }

        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(configPath));
        if (!json.RootElement.TryGetProperty("Discord", out var discord))
        {
            return null;
        }

        if (!discord.TryGetProperty("WebhookUrl", out var webhookUrl))
        {
            return null;
        }

        return webhookUrl.GetString();
    }

    private static ValidationReport CreateReport(int stepCount)
    {
        return new ValidationReport
        {
            GeneratedAt = DateTime.UtcNow,
            Result = new ValidationResult
            {
                TutorialName = "Web Application Development Tutorial",
                TutorialUrl = "https://abp.io/tutorial",
                AbpVersion = "latest",
                Configuration = new TutorialConfiguration
                {
                    Ui = "mvc",
                    Database = "ef",
                    DbProvider = "sqlserver"
                },
                Status = ValidationStatus.Passed,
                StartedAt = DateTime.UtcNow.AddMinutes(-90),
                CompletedAt = DateTime.UtcNow,
                TotalSteps = stepCount,
                PassedSteps = stepCount,
                FailedSteps = 0,
                SkippedSteps = 0,
                StepResults = Enumerable
                    .Range(1, stepCount)
                    .Select(i => new StepResult
                    {
                        StepId = i,
                        StepType = StepType.Command,
                        Status = StepExecutionStatus.Success
                    })
                    .ToList()
            }
        };
    }

    private class RecordingDiscordSender : IDiscordSender
    {
        public List<DiscordMessage> Messages { get; } = [];

        public Task SendAsync(DiscordMessage message, DiscordConfiguration config)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
