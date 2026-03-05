using Validator.Orchestrator.Commands;

namespace Validator.Orchestrator;

/// <summary>
/// Entry point for the Tutorial Validator Orchestrator.
/// Thin dispatcher that routes CLI commands to their handlers.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== Tutorial Validator Orchestrator ===");
        Console.WriteLine();

        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "run" => await RunOrchestratorCommand.RunAsync(args[1..]),
            "docker-only" => await DockerOnlyCommand.RunAsync(args[1..]),
            "analyst-only" => await AnalystOnlyCommand.RunAsync(args[1..]),
            _ => HandleUnknownCommand(command)
        };
    }

    private static int HandleUnknownCommand(string command)
    {
        Console.WriteLine($"Unknown command: {command}");
        Console.WriteLine("Use 'help' to see available commands.");
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            Tutorial Validator Orchestrator

            Commands:
              run           Run the full validation pipeline (analyst + executor)
              docker-only   Run only the executor in Docker (requires testplan)
              analyst-only  Run only the analyst to generate testplan
              help          Show this help message

            Options:
              --url, -u <url>        Tutorial URL to validate
              --testplan, -t <path>  Path to existing testplan.json
              --output, -o <path>    Output directory (default: ./output)
              --config, -c <path>    Path to appsettings.json
              --skip-analyst         Skip analyst, use existing testplan
              --keep-containers      Keep Docker containers after run
              --local                Run executor locally (no Docker)
              --persona <level>      Developer persona: junior, mid, senior (default: mid)
              --timeout <minutes>    Timeout in minutes (default: 60)

            Examples:
              # Full validation pipeline
              dotnet run -- run --url https://abp.io/docs/latest/tutorials/book-store/part-01

              # Skip analyst, use existing testplan
              dotnet run -- run --skip-analyst --testplan ./testplan.json

              # Generate testplan only
              dotnet run -- analyst-only --url https://abp.io/docs/latest/tutorials/book-store/part-01

              # Run executor in Docker with existing testplan
              dotnet run -- docker-only --testplan ./output/testplan.json
            """);
    }
}
