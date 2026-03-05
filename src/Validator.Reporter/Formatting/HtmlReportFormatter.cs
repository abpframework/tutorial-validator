using System.Text;
using System.Text.RegularExpressions;
using Validator.Core.Models.Enums;
using Validator.Core.Models.Results;

namespace Validator.Reporter.Formatting;

/// <summary>
/// Formats validation reports as HTML emails.
/// </summary>
public class HtmlReportFormatter
{
    /// <summary>
    /// Converts a ValidationReport to HTML email content.
    /// </summary>
    /// <param name="report">The validation report to format.</param>
    /// <returns>HTML email content with inline CSS.</returns>
    public string FormatAsHtml(ValidationReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("</head>");
        sb.AppendLine("<body style=\"font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 800px; margin: 0 auto; padding: 20px;\">");

        // Header with status badge
        AppendHeader(sb, report);

        // Summary section
        AppendSummary(sb, report);

        // Step results table
        AppendStepResults(sb, report);

        // Failure diagnostics (if any)
        if (report.FailureDiagnostics?.Count > 0)
        {
            AppendFailureDiagnostics(sb, report);
        }

        // Footer
        AppendFooter(sb, report);

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, ValidationReport report)
    {
        var status = report.Result.Status;
        var badgeColor = status == ValidationStatus.Passed ? "#28a745" : "#dc3545";
        var badgeText = status == ValidationStatus.Passed ? "✓ PASSED" : "✗ FAILED";

        sb.AppendLine("  <div style=\"background-color: #f8f9fa; padding: 20px; border-radius: 5px; margin-bottom: 20px;\">");
        sb.AppendLine("    <h1 style=\"margin: 0 0 10px 0; color: #212529;\">ABP Tutorial Validation Report</h1>");
        sb.AppendLine($"    <div style=\"display: inline-block; background-color: {badgeColor}; color: white; padding: 8px 16px; border-radius: 4px; font-weight: bold;\">{badgeText}</div>");
        sb.AppendLine("  </div>");
    }

    private static void AppendSummary(StringBuilder sb, ValidationReport report)
    {
        sb.AppendLine("  <div style=\"background-color: #ffffff; border: 1px solid #dee2e6; padding: 20px; border-radius: 5px; margin-bottom: 20px;\">");
        sb.AppendLine("    <h2 style=\"margin: 0 0 15px 0; color: #495057; font-size: 1.25rem;\">Summary</h2>");
        
        // Use pre-formatted summary from report
        var summaryLines = report.Summary.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        sb.AppendLine("    <table style=\"width: 100%; border-collapse: collapse;\">");
        foreach (var line in summaryLines)
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine($"        <td style=\"padding: 8px 0; font-weight: bold; width: 150px;\">{parts[0].Trim()}:</td>");
                sb.AppendLine($"        <td style=\"padding: 8px 0;\">{parts[1].Trim()}</td>");
                sb.AppendLine("      </tr>");
            }
        }
        sb.AppendLine("    </table>");
        sb.AppendLine("  </div>");
    }

    private static void AppendStepResults(StringBuilder sb, ValidationReport report)
    {
        sb.AppendLine("  <div style=\"background-color: #ffffff; border: 1px solid #dee2e6; padding: 20px; border-radius: 5px; margin-bottom: 20px;\">");
        sb.AppendLine("    <h2 style=\"margin: 0 0 15px 0; color: #495057; font-size: 1.25rem;\">Step Results</h2>");
        sb.AppendLine("    <table style=\"width: 100%; border-collapse: collapse; font-size: 0.9rem;\">");
        sb.AppendLine("      <thead>");
        sb.AppendLine("        <tr style=\"background-color: #f8f9fa;\">");
        sb.AppendLine("          <th style=\"padding: 10px; text-align: left; border-bottom: 2px solid #dee2e6;\">Step</th>");
        sb.AppendLine("          <th style=\"padding: 10px; text-align: left; border-bottom: 2px solid #dee2e6;\">Description</th>");
        sb.AppendLine("          <th style=\"padding: 10px; text-align: center; border-bottom: 2px solid #dee2e6;\">Status</th>");
        sb.AppendLine("          <th style=\"padding: 10px; text-align: right; border-bottom: 2px solid #dee2e6;\">Duration</th>");
        sb.AppendLine("        </tr>");
        sb.AppendLine("      </thead>");
        sb.AppendLine("      <tbody>");

        foreach (var step in report.Result.StepResults)
        {
            var statusColor = step.Status switch
            {
                StepExecutionStatus.Success => "#28a745",
                StepExecutionStatus.Failed => "#dc3545",
                StepExecutionStatus.Skipped => "#6c757d",
                _ => "#ffc107"
            };

            var statusText = step.Status switch
            {
                StepExecutionStatus.Success => "✓ Success",
                StepExecutionStatus.Failed => "✗ Failed",
                StepExecutionStatus.Skipped => "⊘ Skipped",
                _ => "⋯ Pending"
            };

            var duration = step.Duration?.TotalSeconds.ToString("F2") ?? "—";

            sb.AppendLine("        <tr style=\"border-bottom: 1px solid #dee2e6;\">");
            sb.AppendLine($"          <td style=\"padding: 10px;\">{step.StepId}</td>");
            sb.AppendLine($"          <td style=\"padding: 10px;\">{EscapeHtml(step.Details ?? "N/A")}</td>");
            sb.AppendLine($"          <td style=\"padding: 10px; text-align: center; color: {statusColor}; font-weight: bold;\">{statusText}</td>");
            sb.AppendLine($"          <td style=\"padding: 10px; text-align: right;\">{duration}s</td>");
            sb.AppendLine("        </tr>");

            // Add AI agent report detail row for failed steps
            if (step.Status == StepExecutionStatus.Failed && !string.IsNullOrWhiteSpace(step.Output))
            {
                var formattedOutput = ConvertBasicMarkdownToHtml(step.Output);
                sb.AppendLine("        <tr>");
                sb.AppendLine("          <td colspan=\"4\" style=\"padding: 0 10px 10px 10px; border-bottom: 1px solid #dee2e6;\">");
                sb.AppendLine("            <div style=\"background-color: #fff8f8; border-left: 4px solid #dc3545; padding: 15px; border-radius: 4px; font-size: 0.88rem; line-height: 1.6; word-wrap: break-word; overflow-wrap: break-word;\">");
                sb.AppendLine("              <strong style=\"color: #dc3545;\">AI Agent Report:</strong><br/><br/>");
                sb.AppendLine($"              {formattedOutput}");
                sb.AppendLine("            </div>");
                sb.AppendLine("          </td>");
                sb.AppendLine("        </tr>");
            }
        }

        sb.AppendLine("      </tbody>");
        sb.AppendLine("    </table>");
        sb.AppendLine("  </div>");
    }

    private static void AppendFailureDiagnostics(StringBuilder sb, ValidationReport report)
    {
        sb.AppendLine("  <div style=\"background-color: #fff3cd; border: 1px solid #ffc107; padding: 20px; border-radius: 5px; margin-bottom: 20px;\">");
        sb.AppendLine("    <h2 style=\"margin: 0 0 15px 0; color: #856404; font-size: 1.25rem;\">Failure Diagnostics</h2>");

        foreach (var diagnostic in report.FailureDiagnostics!)
        {
            sb.AppendLine("    <div style=\"background-color: #ffffff; border-left: 4px solid #dc3545; padding: 15px; margin-bottom: 15px; border-radius: 4px;\">");
            sb.AppendLine($"      <h3 style=\"margin: 0 0 10px 0; color: #dc3545; font-size: 1rem;\">Step {diagnostic.StepId}: {EscapeHtml(diagnostic.StepDescription ?? "Unknown")}</h3>");
            sb.AppendLine($"      <p style=\"margin: 5px 0;\"><strong>Classification:</strong> {diagnostic.Classification}</p>");
            sb.AppendLine($"      <p style=\"margin: 5px 0;\"><strong>Explanation:</strong> {EscapeHtml(diagnostic.Explanation)}</p>");
            
            if (!string.IsNullOrEmpty(diagnostic.SuggestedFix))
            {
                sb.AppendLine($"      <p style=\"margin: 5px 0;\"><strong>Suggested Fix:</strong> {EscapeHtml(diagnostic.SuggestedFix)}</p>");
            }

            if (!string.IsNullOrEmpty(diagnostic.ErrorOutput))
            {
                sb.AppendLine("      <details style=\"margin-top: 10px;\">");
                sb.AppendLine("        <summary style=\"cursor: pointer; color: #007bff;\">Error Output</summary>");
                sb.AppendLine($"        <pre style=\"background-color: #f8f9fa; padding: 10px; border-radius: 4px; overflow-x: auto; font-size: 0.85rem;\">{EscapeHtml(diagnostic.ErrorOutput)}</pre>");
                sb.AppendLine("      </details>");
            }

            sb.AppendLine("    </div>");
        }

        sb.AppendLine("  </div>");
    }

    private static void AppendFooter(StringBuilder sb, ValidationReport report)
    {
        sb.AppendLine("  <div style=\"text-align: center; padding: 20px; color: #6c757d; font-size: 0.85rem; border-top: 1px solid #dee2e6;\">");
        sb.AppendLine($"    <p style=\"margin: 5px 0;\">Report generated at {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("    <p style=\"margin: 5px 0;\">Tutorial Validator</p>");
        
        if (!string.IsNullOrEmpty(report.Result.TutorialUrl))
        {
            sb.AppendLine($"    <p style=\"margin: 5px 0;\"><a href=\"{report.Result.TutorialUrl}\" style=\"color: #007bff;\">View Tutorial</a></p>");
        }

        sb.AppendLine("  </div>");
    }

    private static string EscapeHtml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    private static string ConvertBasicMarkdownToHtml(string text)
    {
        var result = EscapeHtml(text);

        // Backtick pairs → <code> tags
        result = Regex.Replace(result, "`([^`]+)`",
            "<code style=\"background-color: #e9ecef; padding: 2px 6px; border-radius: 3px; font-family: monospace; font-size: 0.85em;\">$1</code>");

        // Bold markers → <strong> tags
        result = Regex.Replace(result, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");

        // Newlines → <br/>
        result = result.Replace("\n", "<br/>");

        return result;
    }
}
