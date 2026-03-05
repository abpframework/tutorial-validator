using System.Text.RegularExpressions;
using Validator.Analyst.Scraping.Models;
using Validator.Core.Models;

namespace Validator.Analyst.Analysis;

/// <summary>
/// Detects tutorial configuration (UI, Database, ABP version) from scraped content.
/// </summary>
public static partial class ConfigurationDetector
{
    /// <summary>
    /// Detects tutorial configuration from the scraped tutorial.
    /// Parses URL query parameters and content to determine UI framework, database, and version.
    /// </summary>
    public static TutorialConfiguration DetectConfiguration(ScrapedTutorial tutorial)
    {
        var url = tutorial.SourceUrl;
        
        // Parse UI and DB from URL query parameters (e.g., ?UI=MVC&DB=EF)
        var ui = ExtractUrlParameter(url, "UI") ?? DetectUiFromContent(tutorial);
        var db = ExtractUrlParameter(url, "DB") ?? DetectDbFromContent(tutorial);
        
        return new TutorialConfiguration
        {
            Ui = NormalizeUi(ui),
            Database = NormalizeDatabase(db),
            DbProvider = DetectDbProvider(tutorial, db)
        };
    }

    /// <summary>
    /// Extracts the tutorial name from the scraped tutorial title.
    /// </summary>
    public static string ExtractTutorialName(ScrapedTutorial tutorial)
    {
        return tutorial.Title;
    }

    /// <summary>
    /// Detects the ABP version from tutorial content.
    /// </summary>
    public static string DetectAbpVersion(ScrapedTutorial tutorial)
    {
        // Try to find version from URL (e.g., /docs/9.0/ or /docs/latest/)
        var versionMatch = VersionFromUrlRegex().Match(tutorial.SourceUrl);
        if (versionMatch.Success)
        {
            var version = versionMatch.Groups[1].Value;
            if (version != "latest")
            {
                return version;
            }
        }

        // Search content for version references
        foreach (var page in tutorial.Pages)
        {
            var contentMatch = AbpVersionRegex().Match(page.Content);
            if (contentMatch.Success)
            {
                return contentMatch.Groups[1].Value;
            }
        }

        // Default to latest if not found
        return "latest";
    }

    private static string? ExtractUrlParameter(string url, string paramName)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var query = uri.Query;
        
        if (string.IsNullOrEmpty(query))
            return null;

        var match = Regex.Match(query, $@"[?&]{paramName}=([^&]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string DetectUiFromContent(ScrapedTutorial tutorial)
    {
        var content = GetCombinedContent(tutorial);
        
        if (content.Contains("Blazor", StringComparison.OrdinalIgnoreCase))
            return "blazor";
        if (content.Contains("Angular", StringComparison.OrdinalIgnoreCase))
            return "angular";
        if (content.Contains("MVC", StringComparison.OrdinalIgnoreCase) || 
            content.Contains("Razor Pages", StringComparison.OrdinalIgnoreCase))
            return "mvc";

        return "mvc"; // Default
    }

    private static string DetectDbFromContent(ScrapedTutorial tutorial)
    {
        var content = GetCombinedContent(tutorial);
        
        if (content.Contains("MongoDB", StringComparison.OrdinalIgnoreCase))
            return "mongodb";
        if (content.Contains("Entity Framework", StringComparison.OrdinalIgnoreCase) || 
            content.Contains("EF Core", StringComparison.OrdinalIgnoreCase))
            return "ef";

        return "ef"; // Default
    }

    private static string? DetectDbProvider(ScrapedTutorial tutorial, string db)
    {
        if (db.Equals("mongodb", StringComparison.OrdinalIgnoreCase))
            return null;

        var content = GetCombinedContent(tutorial);
        
        if (content.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            return "postgresql";
        if (content.Contains("MySQL", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Pomelo", StringComparison.OrdinalIgnoreCase))
            return "mysql";
        if (content.Contains("SQLite", StringComparison.OrdinalIgnoreCase))
            return "sqlite";

        return "sqlserver"; // Default for EF
    }

    private static string NormalizeUi(string ui)
    {
        return ui.ToLowerInvariant() switch
        {
            "mvc" or "razorpages" or "razor" => "mvc",
            "blazor" or "blazorserver" or "blazorwasm" or "blazorwebapp" => "blazor",
            "angular" or "ng" => "angular",
            _ => ui.ToLowerInvariant()
        };
    }

    private static string NormalizeDatabase(string db)
    {
        return db.ToLowerInvariant() switch
        {
            "ef" or "efcore" or "entityframework" or "entityframeworkcore" => "ef",
            "mongo" or "mongodb" => "mongodb",
            _ => db.ToLowerInvariant()
        };
    }

    private static string GetCombinedContent(ScrapedTutorial tutorial)
    {
        // Only check first few pages for efficiency
        var pages = tutorial.Pages.Take(3);
        return string.Join(" ", pages.Select(p => p.Content));
    }

    [GeneratedRegex(@"/docs/(\d+\.\d+|\w+)/", RegexOptions.IgnoreCase)]
    private static partial Regex VersionFromUrlRegex();

    [GeneratedRegex(@"ABP\s+(?:Framework\s+)?(?:version\s+)?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex AbpVersionRegex();
}
