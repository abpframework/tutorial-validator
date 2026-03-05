namespace Validator.Analyst.Scraping;

/// <summary>
/// Exception thrown when scraping operations fail.
/// </summary>
public class ScrapingException : Exception
{
    /// <summary>
    /// The URL that was being scraped when the error occurred.
    /// </summary>
    public string? Url { get; }

    /// <summary>
    /// HTTP status code if the error was an HTTP error.
    /// </summary>
    public int? HttpStatusCode { get; }

    /// <summary>
    /// Creates a new ScrapingException.
    /// </summary>
    /// <param name="message">Error message.</param>
    public ScrapingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new ScrapingException with URL context.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="url">The URL being scraped.</param>
    /// <param name="httpStatusCode">Optional HTTP status code.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ScrapingException(
        string message,
        string url,
        int? httpStatusCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Url = url;
        HttpStatusCode = httpStatusCode;
    }
}
