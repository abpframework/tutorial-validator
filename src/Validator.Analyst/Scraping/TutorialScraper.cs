using Validator.Analyst.Scraping.Models;

namespace Validator.Analyst.Scraping;

/// <summary>
/// Orchestrates the scraping of multi-page tutorials.
/// </summary>
public class TutorialScraper : ITutorialScraper
{
    private readonly IHttpFetcher _httpFetcher;
    private readonly IHtmlParser _htmlParser;

    /// <summary>
    /// Creates a new TutorialScraper with the specified dependencies.
    /// </summary>
    /// <param name="httpFetcher">HTTP fetcher for downloading pages.</param>
    /// <param name="htmlParser">HTML parser for extracting content.</param>
    public TutorialScraper(IHttpFetcher httpFetcher, IHtmlParser htmlParser)
    {
        _httpFetcher = httpFetcher ?? throw new ArgumentNullException(nameof(httpFetcher));
        _htmlParser = htmlParser ?? throw new ArgumentNullException(nameof(htmlParser));
    }

    /// <summary>
    /// Creates a new TutorialScraper with default implementations.
    /// </summary>
    public TutorialScraper() : this(new HttpFetcher(), new HtmlParser())
    {
    }

    /// <inheritdoc/>
    public async Task<ScrapedTutorial> ScrapeAsync(
        string startUrl,
        int maxPages = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(startUrl))
        {
            throw new ArgumentException("Start URL cannot be null or empty.", nameof(startUrl));
        }

        if (maxPages < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPages), "Max pages must be at least 1.");
        }

        var pages = new List<TutorialPage>();
        var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentUrl = startUrl;
        var pageIndex = 0;
        string? tutorialTitle = null;

        while (!string.IsNullOrEmpty(currentUrl) && pageIndex < maxPages)
        {
            // Prevent infinite loops
            if (!visitedUrls.Add(currentUrl))
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            Console.WriteLine($"  Scraping page {pageIndex + 1}: {currentUrl}");

            try
            {
                var html = await _httpFetcher.FetchAsync(currentUrl, cancellationToken);
                var parseResult = _htmlParser.Parse(html, currentUrl);

                // Use first page title as tutorial title
                tutorialTitle ??= parseResult.Title;

                var page = new TutorialPage
                {
                    Url = currentUrl,
                    Title = parseResult.Title,
                    PageIndex = pageIndex,
                    Content = parseResult.Content,
                    ImageUrls = parseResult.ImageUrls,
                    NextPageUrl = parseResult.NextPageUrl,
                    PreviousPageUrl = parseResult.PreviousPageUrl
                };

                pages.Add(page);
                pageIndex++;

                // Move to next page if available
                currentUrl = parseResult.NextPageUrl!;
            }
            catch (ScrapingException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ScrapingException(
                    $"Unexpected error scraping page {pageIndex + 1} at {currentUrl}: {ex.Message}",
                    currentUrl,
                    innerException: ex);
            }
        }

        if (pages.Count == 0)
        {
            throw new ScrapingException($"No pages were scraped from {startUrl}", startUrl);
        }

        return new ScrapedTutorial
        {
            Title = tutorialTitle ?? "Untitled Tutorial",
            SourceUrl = startUrl,
            Pages = pages
        };
    }
}
