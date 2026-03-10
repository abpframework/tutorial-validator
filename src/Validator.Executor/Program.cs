using Validator.Executor.Commands;

// Entry point for the tutorial executor
if (args.Length == 0)
{
    ShowHelp();
    return;
}

// Determine command
var command = args[0].ToLowerInvariant();

switch (command)
{
    case "run":
        await RunCommand.RunAsync(args[1..]);
        break;
    case "--help":
    case "-h":
    case "help":
        ShowHelp();
        break;
    default:
        // If first arg looks like a file path, assume run command
        if (args[0].EndsWith(".json", StringComparison.OrdinalIgnoreCase) || args[0] == "--input")
        {
            await RunCommand.RunAsync(args);
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
    Console.WriteLine("Tutorial Validator - Executor (AI Agent)");
    Console.WriteLine();
    Console.WriteLine("The Executor is an AI agent that acts as a naive user following a tutorial.");
    Console.WriteLine("It executes each step EXACTLY as written, without using its knowledge to fix problems.");
    Console.WriteLine("If a step fails, it reports the failure - this indicates the tutorial has issues.");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine();
    Console.WriteLine("  run       Execute a TestPlan using the AI agent");
    Console.WriteLine("  help      Show this help message");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine();
    Console.WriteLine("  Execute a TestPlan:");
    Console.WriteLine("    dotnet run -- run --input <testplan.json> --workdir <directory> [options]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine();
    Console.WriteLine("  --input <path>     Path to the testplan.json file (required)");
    Console.WriteLine("  --workdir <path>   Working directory for tutorial execution (default: current directory)");
    Console.WriteLine("  --output <path>    Output directory for results (default: ./results)");
    Console.WriteLine("  --persona <level>  Developer persona: junior, mid, senior (default: mid)");
    Console.WriteLine("  --dry-run          Show what would be executed without actually running");
    Console.WriteLine("  --config <path>    Path to appsettings.json for AI configuration");
    Console.WriteLine();
    Console.WriteLine("Personas:");
    Console.WriteLine();
    Console.WriteLine("  junior   Basic programming familiarity, limited C#/.NET knowledge.");
    Console.WriteLine("           Follows instructions literally with zero code intelligence.");
    Console.WriteLine("           Will not add usings, fix syntax, or do smart merges.");
    Console.WriteLine();
    Console.WriteLine("  mid      Familiar with C# and .NET, new to ABP Framework (default).");
    Console.WriteLine("           Can ensure syntactic validity but won't use ABP knowledge.");
    Console.WriteLine();
    Console.WriteLine("  senior   Expert in C#, .NET, and ABP Framework.");
    Console.WriteLine("           Can self-fix errors, diagnose build failures, and retry.");
    Console.WriteLine();
    Console.WriteLine("Environment Variables (for AI configuration):");
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
    Console.WriteLine("  OpenAI-Compatible:");
    Console.WriteLine("    OPENAI_COMPAT_BASE_URL    Provider base URL (e.g. https://api.example.com/v1)");
    Console.WriteLine("    OPENAI_COMPAT_API_KEY     API key");
    Console.WriteLine("    OPENAI_COMPAT_MODEL       Model name");
    Console.WriteLine("    OPENAI_COMPAT_ORG         Optional organization ID");
    Console.WriteLine("    OPENAI_COMPAT_PROJECT     Optional project ID");
    Console.WriteLine("    AI_PROVIDER               Force provider: OpenAI, AzureOpenAI, OpenAICompatible");
    Console.WriteLine("    EXECUTOR_BUILD_GATE_INTERVAL  Senior-only build gate interval (0=disabled, default: 0)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine();
    Console.WriteLine("  # Execute a test plan with AI agent");
    Console.WriteLine("  dotnet run -- run --input testplan.json --workdir ./workspace");
    Console.WriteLine();
    Console.WriteLine("  # Dry-run to see what would happen");
    Console.WriteLine("  dotnet run -- run --input testplan.json --workdir ./workspace --dry-run");
}
