using Validator.Analyst.Scraping.Models;

namespace Validator.Analyst.Scraping;

/// <summary>
/// Main interface for scraping tutorials from URLs.
/// Handles multi-page tutorials by following navigation links.
/// </summary>
public interface ITutorialScraper
{
    /// <summary>
    /// Scrapes a tutorial starting from the given URL.
    /// Automatically follows "Next" links for multi-page tutorials.
    /// </summary>
    /// <param name="startUrl">The URL of the first tutorial page.</param>
    /// <param name="maxPages">Maximum number of pages to scrape (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The scraped tutorial with all pages.</returns>
    /// <exception cref="ScrapingException">Thrown when scraping fails.</exception>
    Task<ScrapedTutorial> ScrapeAsync(
        string startUrl,
        int maxPages = 20,
        CancellationToken cancellationToken = default);
}
