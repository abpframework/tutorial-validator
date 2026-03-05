using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Validator.Core;
using Validator.Core.Models.Results;
using Validator.Orchestrator.Models;
using Validator.Reporter;
using Validator.Reporter.Email;

namespace Validator.Orchestrator.Runners;

/// <summary>
/// Handles sending validation reports via email when enabled in configuration.
/// </summary>
internal class EmailReportSender
{
    private readonly IConfiguration _configuration;

    internal EmailReportSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Sends the validation report via email if email reporting is enabled in configuration.
    /// </summary>
    /// <param name="outputPath">Output directory containing validation results.</param>
    /// <param name="summary">The orchestration summary.</param>
    internal async Task SendIfEnabledAsync(string outputPath, OrchestrationSummary summary)
    {
        try
        {
            // Load email configuration
            var enabledValue = _configuration.GetValue<bool>("Email:Enabled");

            var emailConfig = new EmailConfiguration
            {
                Enabled = enabledValue,
                SmtpHost = _configuration.GetValue<string>("Email:SmtpHost") ?? "smtp.example.com",
                SmtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587),
                UseSsl = _configuration.GetValue<bool>("Email:UseSsl", true),
                Username = _configuration.GetValue<string>("Email:Username"),
                Password = _configuration.GetValue<string>("Email:Password"),
                FromAddress = _configuration.GetValue<string>("Email:FromAddress") ?? "tutorial-validator@example.com",
                FromName = _configuration.GetValue<string>("Email:FromName") ?? "Tutorial Validator",
                ToAddresses = _configuration.GetSection("Email:ToAddresses").Get<List<string>>() ?? new List<string>(),
                CcAddresses = _configuration.GetSection("Email:CcAddresses").Get<List<string>>()
            };

            if (!emailConfig.Enabled)
            {
                Console.WriteLine("Email reporting is disabled in configuration.");
                return;
            }

            // Load validation report
            var validationReportPath = Path.Combine(outputPath, "results", "validation-report.json");
            if (!File.Exists(validationReportPath))
            {
                Console.WriteLine("Warning: validation-report.json not found. Skipping email notification.");
                return;
            }

            var json = await File.ReadAllTextAsync(validationReportPath);
            var validationReport = JsonSerializer.Deserialize<ValidationReport>(json, JsonSerializerOptionsProvider.Default);

            if (validationReport == null)
            {
                Console.WriteLine("Warning: Failed to deserialize validation report. Skipping email notification.");
                return;
            }

            // Send email report
            IReportNotifier notifier = new EmailReportNotifier(emailConfig);
            await notifier.SendReportAsync(validationReport);
            summary.EmailSent = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to send email report: {ex.Message}");
            Console.WriteLine("Continuing without email notification...");
            // Don't fail the pipeline if email sending fails
        }
    }
}
