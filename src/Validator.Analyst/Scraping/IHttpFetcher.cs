namespace Validator.Analyst.Scraping;

/// <summary>
/// Abstraction for fetching HTTP content.
/// Enables testing without actual network calls.
/// </summary>
public interface IHttpFetcher
{
    /// <summary>
    /// Fetches the HTML content from the specified URL.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The HTML content as a string.</returns>
    /// <exception cref="ScrapingException">Thrown when the fetch fails.</exception>
    Task<string> FetchAsync(string url, CancellationToken cancellationToken = default);
}
