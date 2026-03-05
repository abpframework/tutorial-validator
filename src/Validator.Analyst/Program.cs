using Validator.Analyst.Commands;

// Entry point for the tutorial analyzer
if (args.Length == 0)
{
    ShowHelp();
    return;
}

// Determine command
var command = args[0].ToLowerInvariant();

switch (command)
{
    case "scrape":
        await ScrapeCommand.RunAsync(args[1..]);
        break;
    case "analyze":
        await AnalyzeCommand.RunAsync(args[1..]);
        break;
    case "full":
        await FullPipelineCommand.RunAsync(args[1..]);
        break;
    case "--help":
    case "-h":
    case "help":
        ShowHelp();
        break;
    default:
        // Legacy support: if first arg looks like a URL, assume scrape command
        if (args[0].StartsWith("http") || args[0] == "--url")
        {
            await ScrapeCommand.RunAsync(args);
        }
        else
        {
            Console.Error.WriteLine($"Unknown command: {command}");
            Console.Error.WriteLine("Use 'help' to see available commands.");
            Environment.Exit(1);
        }
        break;
}

void ShowHelp()
{
    Console.WriteLine("Tutorial Validator - Analyst");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine();
    Console.WriteLine("  scrape    Scrape a tutorial from URL to markdown");
    Console.WriteLine("  analyze   Analyze scraped content and generate TestPlan");
    Console.WriteLine("  full      Run both scrape and analyze in sequence");
    Console.WriteLine("  help      Show this help message");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine();
    Console.WriteLine("  Scrape a tutorial:");
    Console.WriteLine("    dotnet run -- scrape --url \"<tutorial-url>\" [--max-pages <n>] [--output <dir>]");
    Console.WriteLine();
    Console.WriteLine("  Analyze scraped content:");
    Console.WriteLine("    dotnet run -- analyze --input <scraped-dir-or-json> [--output <testplan.json>] [--target-steps <n>] [--max-steps <n>]");
    Console.WriteLine();
    Console.WriteLine("  Full pipeline (scrape + analyze):");
    Console.WriteLine("    dotnet run -- full --url \"<tutorial-url>\" [--output <dir>] [--target-steps <n>] [--max-steps <n>]");
    Console.WriteLine();
    Console.WriteLine("Environment Variables for AI Configuration:");
    Console.WriteLine();
    Console.WriteLine("  Azure OpenAI:");
    Console.WriteLine("    AZURE_OPENAI_ENDPOINT     Azure OpenAI endpoint URL");
    Console.WriteLine("    AZURE_OPENAI_API_KEY      Azure OpenAI API key");
    Console.WriteLine("    AZURE_OPENAI_DEPLOYMENT   Deployment name (default: gpt-4o)");
    Console.WriteLine();
    Console.WriteLine("  OpenAI:");
    Console.WriteLine("    OPENAI_API_KEY            OpenAI API key");
    Console.WriteLine("    OPENAI_MODEL              Model name (default: gpt-4o)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine();
    Console.WriteLine("  # Scrape a tutorial");
    Console.WriteLine("  dotnet run -- scrape --url \"https://abp.io/docs/latest/tutorials/book-store?UI=MVC&DB=EF\"");
    Console.WriteLine();
    Console.WriteLine("  # Analyze previously scraped content");
    Console.WriteLine("  dotnet run -- analyze --input ScrapedContent");
    Console.WriteLine();
    Console.WriteLine("  # Full pipeline");
    Console.WriteLine("  dotnet run -- full --url \"https://abp.io/docs/latest/tutorials/book-store?UI=MVC&DB=EF\" --output Output");
}
