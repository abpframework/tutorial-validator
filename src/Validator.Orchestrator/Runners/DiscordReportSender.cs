using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Validator.Core;
using Validator.Core.Models.Results;
using Validator.Orchestrator.Models;
using Validator.Reporter;
using Validator.Reporter.Discord;

namespace Validator.Orchestrator.Runners;

/// <summary>
/// Handles sending validation reports via Discord webhook when enabled in configuration.
/// </summary>
internal class DiscordReportSender
{
    private readonly IConfiguration _configuration;

    internal DiscordReportSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Sends the validation report via Discord webhook if Discord reporting is enabled in configuration.
    /// </summary>
    /// <param name="outputPath">Output directory containing validation results.</param>
    /// <param name="summary">The orchestration summary.</param>
    internal async Task SendIfEnabledAsync(string outputPath, OrchestrationSummary summary)
    {
        try
        {
            var discordConfig = new DiscordConfiguration
            {
                Enabled = _configuration.GetValue<bool>("Discord:Enabled"),
                WebhookUrl = _configuration.GetValue<string>("Discord:WebhookUrl") ?? string.Empty
            };

            if (!discordConfig.Enabled)
            {
                Console.WriteLine("Discord reporting is disabled in configuration.");
                return;
            }

            // Load validation report
            var validationReportPath = Path.Combine(outputPath, "results", "validation-report.json");
            if (!File.Exists(validationReportPath))
            {
                Console.WriteLine("Warning: validation-report.json not found. Skipping Discord notification.");
                return;
            }

            var json = await File.ReadAllTextAsync(validationReportPath);
            var validationReport = JsonSerializer.Deserialize<ValidationReport>(json, JsonSerializerOptionsProvider.Default);

            if (validationReport == null)
            {
                Console.WriteLine("Warning: Failed to deserialize validation report. Skipping Discord notification.");
                return;
            }

            // Send Discord report
            IReportNotifier notifier = new DiscordReportNotifier(discordConfig);
            await notifier.SendReportAsync(validationReport);
            summary.DiscordSent = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to send Discord report: {ex.Message}");
            Console.WriteLine("Continuing without Discord notification...");
            // Don't fail the pipeline if Discord sending fails
        }
    }
}
