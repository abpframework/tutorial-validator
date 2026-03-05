using System.Text;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Results;
using Validator.Reporter.Discord;

namespace Validator.Reporter.Formatting;

/// <summary>
/// Formats validation reports as Discord webhook messages using rich embeds.
/// </summary>
public class DiscordReportFormatter
{
    // Discord embed color values (decimal)
    private const int ColorGreen = 0x00CC44;
    private const int ColorRed = 0xCC0000;

    // Discord limits
    private const int MaxEmbedDescriptionLength = 4096;
    private const int MaxFieldValueLength = 1024;
    private const int MaxTotalEmbedLength = 6000;

    // Step status icons
    private static readonly Dictionary<StepExecutionStatus, string> StepIcons = new()
    {
        [StepExecutionStatus.Success] = "✅",
        [StepExecutionStatus.Failed]  = "❌",
        [StepExecutionStatus.Skipped] = "⏭️",
        [StepExecutionStatus.Pending] = "⏳",
        [StepExecutionStatus.Running] = "🔄"
    };

    /// <summary>
    /// Converts a ValidationReport to a DiscordMessage with rich embeds.
    /// </summary>
    /// <param name="report">The validation report to format.</param>
    /// <returns>A DiscordMessage ready to be sent via webhook.</returns>
    public DiscordMessage Format(ValidationReport report)
    {
        return FormatMessages(report)[0];
    }

    /// <summary>
    /// Builds the single-line Discord notification content requested for pipeline reporting.
    /// </summary>
    /// <param name="report">The validation report to format.</param>
    /// <returns>A single Discord message with plain-text content.</returns>
    public DiscordMessage FormatNotificationMessage(ValidationReport report)
    {
        var failedStepDetails = BuildFailedStepDetails(report);
        if (failedStepDetails is null)
        {
            var totalSteps = report.Result.TotalSteps > 0 ? report.Result.TotalSteps : report.Result.StepResults.Count;
            return new DiscordMessage
            {
                Content = $"All steps ({totalSteps}) successfully passed"
            };
        }

        return new DiscordMessage
        {
            Content = failedStepDetails
        };
    }

    /// <summary>
    /// Converts a ValidationReport to one or more Discord messages.
    /// </summary>
    /// <param name="report">The validation report to format.</param>
    /// <returns>Ordered Discord webhook payloads.</returns>
    public List<DiscordMessage> FormatMessages(ValidationReport report)
    {
        var isPassed = report.Result.Status == ValidationStatus.Passed;
        var color = isPassed ? ColorGreen : ColorRed;
        var statusIcon = isPassed ? "✅" : "❌";
        var statusText = isPassed ? "PASSED" : "FAILED";

        var messages = new List<DiscordMessage>();

        var summaryEmbed = new DiscordEmbed
        {
            Title = $"{statusIcon} Tutorial Validation — {statusText}",
            Color = color,
            Timestamp = report.GeneratedAt.ToString("o"),
            Footer = new DiscordEmbedFooter
            {
                Text = "Tutorial Validator"
            }
        };

        // Summary fields (inline grid)
        summaryEmbed.Fields.Add(new DiscordEmbedField
        {
            Name = "Tutorial",
            Value = Truncate(report.Result.TutorialName, MaxFieldValueLength),
            Inline = false
        });

        summaryEmbed.Fields.Add(new DiscordEmbedField
        {
            Name = "ABP Version",
            Value = report.Result.AbpVersion,
            Inline = true
        });

        summaryEmbed.Fields.Add(new DiscordEmbedField
        {
            Name = "UI / Database",
            Value = $"{report.Result.Configuration.Ui} / {report.Result.Configuration.Database}",
            Inline = true
        });

        var duration = report.Result.Duration?.TotalMinutes.ToString("F1") ?? "N/A";
        summaryEmbed.Fields.Add(new DiscordEmbedField
        {
            Name = "Duration",
            Value = $"{duration} min",
            Inline = true
        });

        summaryEmbed.Fields.Add(new DiscordEmbedField
        {
            Name = "Steps",
            Value = $"{report.Result.PassedSteps} passed / {report.Result.FailedSteps} failed / {report.Result.SkippedSteps} skipped",
            Inline = true
        });

        summaryEmbed.Fields.Add(new DiscordEmbedField
        {
            Name = "Status",
            Value = statusText,
            Inline = true
        });

        var failedStepDetails = BuildFailedStepDetails(report);
        if (failedStepDetails is not null)
        {
            summaryEmbed.Fields.Add(new DiscordEmbedField
            {
                Name = "Failed Step",
                Value = Truncate(failedStepDetails, MaxFieldValueLength),
                Inline = false
            });
        }

        messages.Add(new DiscordMessage
        {
            Embeds = [summaryEmbed]
        });

        AppendSectionMessages(messages, BuildDiagnosticsChunks(report), "Failure Diagnostics", color, report.GeneratedAt);

        return messages;
    }

    private static List<string> BuildStepsChunks(ValidationReport report)
    {
        if (report.Result.StepResults.Count == 0)
        {
            return [];
        }

        var lines = new List<string>();

        foreach (var step in report.Result.StepResults)
        {
            var icon = StepIcons.TryGetValue(step.Status, out var i) ? i : "•";
            var line = $"{icon} Step {step.StepId}: {step.StepType}";

            if (step.Status == StepExecutionStatus.Failed && !string.IsNullOrWhiteSpace(step.ErrorMessage))
            {
                line += $" — `{step.ErrorMessage.Trim()}`";
            }

            lines.Add(line);
        }

        return BuildFieldChunks(lines);
    }

    private static List<string> BuildDiagnosticsChunks(ValidationReport report)
    {
        if (report.FailureDiagnostics?.Count is not > 0)
        {
            return [];
        }

        var lines = new List<string>();

        foreach (var diag in report.FailureDiagnostics!)
        {
            var stepLabel = diag.StepDescription is not null
                ? $"Step {diag.StepId}: {Truncate(diag.StepDescription, 60)}"
                : $"Step {diag.StepId}";

            lines.Add($"**{stepLabel}**");
            lines.Add($"Classification: `{diag.Classification}`");
            lines.Add(diag.Explanation);

            if (!string.IsNullOrWhiteSpace(diag.SuggestedFix))
            {
                lines.Add($"💡 {diag.SuggestedFix}");
            }

            lines.Add(string.Empty);
        }

        return BuildFieldChunks(lines);
    }

    private static List<string> BuildFieldChunks(List<string> lines)
    {
        var chunks = new List<string>();
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            foreach (var segment in SplitByLength(line, MaxFieldValueLength))
            {
                var requiredLength = segment.Length + (sb.Length > 0 ? 1 : 0);
                if (sb.Length > 0 && sb.Length + requiredLength > MaxFieldValueLength)
                {
                    chunks.Add(sb.ToString());
                    sb.Clear();
                }

                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(segment);
            }
        }

        if (sb.Length > 0)
        {
            chunks.Add(sb.ToString());
        }

        return chunks;
    }

    private static IEnumerable<string> SplitByLength(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            yield return string.Empty;
            yield break;
        }

        for (var i = 0; i < value.Length; i += maxLength)
        {
            var length = Math.Min(maxLength, value.Length - i);
            yield return value.Substring(i, length);
        }
    }

    private static void AppendSectionMessages(
        List<DiscordMessage> messages,
        List<string> chunks,
        string sectionName,
        int color,
        DateTime generatedAt)
    {
        for (var i = 0; i < chunks.Count; i++)
        {
            var part = chunks.Count > 1 ? $" ({i + 1}/{chunks.Count})" : string.Empty;
            var embed = new DiscordEmbed
            {
                Title = $"{sectionName}{part}",
                Color = color,
                Timestamp = generatedAt.ToString("o"),
                Footer = new DiscordEmbedFooter
                {
                    Text = "Tutorial Validator"
                }
            };

            embed.Fields.Add(new DiscordEmbedField
            {
                Name = sectionName,
                Value = chunks[i],
                Inline = false
            });

            messages.Add(new DiscordMessage
            {
                Embeds = [embed]
            });
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }

    private static string? BuildFailedStepDetails(ValidationReport report)
    {
        var totalSteps = report.Result.TotalSteps > 0 ? report.Result.TotalSteps : report.Result.StepResults.Count;
        var failedStepId = report.Result.FailedAtStepId;
        StepResult? failedStep = null;

        if (failedStepId.HasValue)
        {
            failedStep = report.Result.StepResults.FirstOrDefault(s => s.StepId == failedStepId.Value);
        }

        failedStep ??= report.Result.StepResults.FirstOrDefault(s => s.Status == StepExecutionStatus.Failed);
        failedStepId ??= failedStep?.StepId;

        if (!failedStepId.HasValue)
        {
            return null;
        }

        var errorText = failedStep?.ErrorMessage;
        if (string.IsNullOrWhiteSpace(errorText))
        {
            errorText = failedStep?.Details;
        }

        if (string.IsNullOrWhiteSpace(errorText))
        {
            errorText = failedStep?.ErrorOutput;
        }

        errorText = string.IsNullOrWhiteSpace(errorText)
            ? "Unknown error"
            : errorText.Replace('\r', ' ').Replace('\n', ' ').Trim();

        return $"Error on step {failedStepId}/{totalSteps}: {errorText}";
    }
}
