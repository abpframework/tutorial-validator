using Validator.Analyst.Analysis;
using Validator.Analyst.Scraping;
using Validator.Core;

namespace Validator.Analyst.Commands;

/// <summary>
/// Handles the "full" CLI command.
/// Runs both scrape and analyze in sequence.
/// </summary>
internal static class FullPipelineCommand
{
    /// <summary>
    /// Runs the full pipeline command with the provided arguments.
    /// </summary>
    internal static async Task RunAsync(string[] args)
    {
        string? url = null;
        var maxPages = 20;
        var outputDir = "Output";
        string? configPath = null;
        var targetSteps = 50;
        var maxSteps = 55;

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
                case "--config" when i + 1 < args.Length:
                    configPath = args[++i];
                    break;
                case "--target-steps" when i + 1 < args.Length:
                    targetSteps = int.Parse(args[++i]);
                    break;
                case "--max-steps" when i + 1 < args.Length:
                    maxSteps = int.Parse(args[++i]);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            Console.Error.WriteLine("Error: URL is required.");
            Console.Error.WriteLine("Usage: dotnet run -- full --url \"<tutorial-url>\"");
            Environment.Exit(1);
            return;
        }

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        ConsoleFormatter.WritePhaseHeader("Full Pipeline");
        Console.WriteLine($"URL: {url}");
        Console.WriteLine($"Output: {outputDir}");

        try
        {
            // Step 1: Scrape
            ConsoleFormatter.WritePhaseHeader("Step 1: Scraping");

            var scraper = new TutorialScraper();
            var tutorial = await scraper.ScrapeAsync(url, maxPages);

            Console.WriteLine($"Scraped {tutorial.TotalPages} pages");

            // Save scraped content
            var scrapedDir = Path.Combine(outputDir, "scraped");
            Directory.CreateDirectory(scrapedDir);

            await CommandHelpers.SaveScrapedContentAsync(tutorial, scrapedDir);

            Console.WriteLine($"Scraped content saved to: {scrapedDir}");
            Console.WriteLine();

            // Step 2: Analyze
            ConsoleFormatter.WritePhaseHeader("Step 2: Analyzing");

            // Load AI configuration
            var aiConfig = CommandHelpers.LoadAndValidateAIConfig(configPath);

            Console.WriteLine($"Using AI provider: {aiConfig.Provider}");
            Console.WriteLine();

            var kernel = KernelFactory.CreateFromConfiguration(aiConfig);
            var analyzer = new TutorialAnalyzer(kernel, new StepCompactionOptions
            {
                TargetStepCount = targetSteps,
                MaxStepCount = maxSteps
            });
            var testPlan = await analyzer.AnalyzeAsync(tutorial);

            // Save TestPlan
            var testPlanPath = Path.Combine(outputDir, "testplan.json");
            await TutorialAnalyzer.SaveTestPlanAsync(testPlan, testPlanPath);

            ConsoleFormatter.WritePhaseHeader("Pipeline Complete");
            Console.WriteLine($"Scraped content: {scrapedDir}");
            Console.WriteLine($"TestPlan: {testPlanPath}");
            Console.WriteLine($"Total steps: {testPlan.Steps.Count}");
        }
        catch (ScrapingException ex)
        {
            ConsoleFormatter.WritePhaseHeader("Scraping Failed");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            ConsoleFormatter.WritePhaseHeader("Pipeline Failed");
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}
