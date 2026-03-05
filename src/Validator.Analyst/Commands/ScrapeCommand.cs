using Validator.Analyst.Scraping;
using Validator.Core;

namespace Validator.Analyst.Commands;

/// <summary>
/// Handles the "scrape" CLI command.
/// Scrapes a tutorial from a URL and saves content as markdown.
/// </summary>
internal static class ScrapeCommand
{
    /// <summary>
    /// Runs the scrape command with the provided arguments.
    /// </summary>
    internal static async Task RunAsync(string[] args)
    {
        string? url = null;
        var maxPages = 20;
        var outputDir = "ScrapedContent";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length:
                    url = args[++i];
                    break;
                case "--max-pages" when i + 1 < args.Length:
                    maxPages = int.Parse(args[++i]);
                    break;
                case "--output" when i + 1 < args.Length:
                    outputDir = args[++i];
                    break;
                default:
                    // Support legacy positional URL argument if it looks like a URL
                    if (url == null && args[i].StartsWith("http"))
                    {
                        url = args[i];
                    }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            Console.Error.WriteLine("Error: URL is required.");
            Console.Error.WriteLine("Usage: dotnet run -- scrape --url \"<tutorial-url>\"");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Make sure to wrap the URL in double quotes!");
            Environment.Exit(1);
            return;
        }

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        Console.WriteLine($"Scraping tutorial from: {url}");
        Console.WriteLine($"Max pages: {maxPages}");
        Console.WriteLine($"Output directory: {outputDir}");
        Console.WriteLine();

        var scraper = new TutorialScraper();

        try
        {
            var tutorial = await scraper.ScrapeAsync(url, maxPages);

            // Summary
            ConsoleFormatter.WritePhaseHeader("Scraping Complete");
            Console.WriteLine($"Title: {tutorial.Title}");
            Console.WriteLine($"Pages: {tutorial.TotalPages}");
            Console.WriteLine($"Scraped at: {tutorial.ScrapedAt:u}");

            // Page details
            ConsoleFormatter.WritePhaseHeader("Pages");
            foreach (var page in tutorial.Pages)
            {
                Console.WriteLine($"Page {page.PageIndex + 1}: {page.Title}");
                Console.WriteLine($"  URL: {page.Url}");
                Console.WriteLine($"  Content length: {page.Content.Length} chars");
                Console.WriteLine($"  Images: {page.ImageUrls.Count}");
                if (page.NextPageUrl != null)
                {
                    Console.WriteLine($"  Next: {page.NextPageUrl}");
                }
                Console.WriteLine();
            }

            await CommandHelpers.SaveScrapedContentAsync(tutorial, outputDir);

            Console.WriteLine();
            Console.WriteLine("Scraping complete. Run 'analyze' command to generate TestPlan.");
        }
        catch (ScrapingException ex)
        {
            ConsoleFormatter.WritePhaseHeader("Scraping Failed");
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.Url != null)
            {
                Console.Error.WriteLine($"URL: {ex.Url}");
            }
            if (ex.HttpStatusCode.HasValue)
            {
                Console.Error.WriteLine($"HTTP Status: {ex.HttpStatusCode}");
            }
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleFormatter.WritePhaseHeader("Unexpected Error");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
