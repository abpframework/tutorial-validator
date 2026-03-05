using Validator.Core.Models.Results;

namespace Validator.Reporter;

/// <summary>
/// Channel-agnostic interface for sending validation report notifications.
/// </summary>
public interface IReportNotifier
{
    /// <summary>
    /// Sends a validation report notification asynchronously.
    /// </summary>
    /// <param name="report">The validation report to send.</param>
    Task SendReportAsync(ValidationReport report);
}
