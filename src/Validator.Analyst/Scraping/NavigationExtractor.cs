using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace Validator.Analyst.Scraping;

/// <summary>
/// Detects next/previous page navigation links in HTML documents.
/// </summary>
internal static class NavigationExtractor
{
    /// <summary>
    /// Extracts navigation links (next/previous) from the document.
    /// </summary>
    internal static (string? NextUrl, string? PrevUrl) ExtractNavigationLinks(IDocument document, string sourceUrl)
    {
        string? nextUrl = null;
        string? prevUrl = null;

        // Extract query parameters from source URL to preserve them
        var queryString = ExtractQueryString(sourceUrl);

        var allLinks = document.QuerySelectorAll("a");

        foreach (var link in allLinks)
        {
            var href = link.GetAttribute("href");
            if (string.IsNullOrEmpty(href) || href.StartsWith("javascript:"))
            {
                continue;
            }

            var text = link.TextContent.Trim();
            var textLower = text.ToLowerInvariant();
            var ariaLabel = link.GetAttribute("aria-label")?.ToLowerInvariant() ?? "";
            var title = link.GetAttribute("title")?.ToLowerInvariant() ?? "";
            var rel = link.GetAttribute("rel")?.ToLowerInvariant() ?? "";
            var className = link.ClassName?.ToLowerInvariant() ?? "";

            // Skip variant switcher links (UI=...&DB=...)
            if (href.Contains("UI=") && href.Contains("DB="))
            {
                continue;
            }

            // Check for "next" indicators - ABP docs pattern
            if (nextUrl == null)
            {
                var isNext = textLower.StartsWith("next") ||
                             text.Contains("→") ||
                             ariaLabel.Contains("next") ||
                             title.Contains("next") ||
                             rel == "next" ||
                             className.Contains("next");

                // Also check for part-XX pattern in href (for ABP tutorial parts)
                var isPartLink = href.Contains("/part-") && !href.Contains(GetCurrentPart(sourceUrl));

                if (isNext || (isPartLink && IsNextPart(sourceUrl, href)))
                {
                    nextUrl = ResolveUrlWithQueryParams(href, sourceUrl, queryString);
                }
            }

            // Check for "previous" indicators
            if (prevUrl == null)
            {
                var isPrev = textLower.StartsWith("prev") ||
                             textLower.StartsWith("back") ||
                             text.Contains("←") ||
                             ariaLabel.Contains("prev") ||
                             title.Contains("prev") ||
                             rel == "prev" ||
                             className.Contains("prev");

                if (isPrev)
                {
                    prevUrl = ResolveUrlWithQueryParams(href, sourceUrl, queryString);
                }
            }
        }

        return (nextUrl, prevUrl);
    }

    /// <summary>
    /// Resolves a relative URL to an absolute URL.
    /// </summary>
    internal static string ResolveUrl(string href, string baseUrl)
    {
        // Only treat as absolute if it's an http/https URL.
        // On Linux, Uri.TryCreate("/path", UriKind.Absolute) succeeds and
        // returns file:///path — we must not accept that as a resolved URL.
        if (Uri.TryCreate(href, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return absoluteUri.ToString();
        }

        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
            Uri.TryCreate(baseUri, href, out var resolvedUri))
        {
            return resolvedUri.ToString();
        }

        return href;
    }

    /// <summary>
    /// Extracts query string from URL (e.g., "?UI=MVC&amp;DB=EF" -> "UI=MVC&amp;DB=EF")
    /// </summary>
    private static string ExtractQueryString(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.Query.TrimStart('?');
        }
        
        var queryIndex = url.IndexOf('?');
        return queryIndex >= 0 ? url[(queryIndex + 1)..] : "";
    }

    /// <summary>
    /// Gets the current part number from URL (e.g., "/part-01" -> "part-01")
    /// </summary>
    private static string GetCurrentPart(string url)
    {
        var match = Regex.Match(url, @"part-(\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Value : "";
    }

    /// <summary>
    /// Checks if the href points to the next sequential part.
    /// </summary>
    private static bool IsNextPart(string currentUrl, string href)
    {
        var currentMatch = Regex.Match(currentUrl, @"part-(\d+)", RegexOptions.IgnoreCase);
        var hrefMatch = Regex.Match(href, @"part-(\d+)", RegexOptions.IgnoreCase);

        if (!currentMatch.Success)
        {
            // Current URL is overview, any part-XX is next
            return hrefMatch.Success;
        }

        if (currentMatch.Success && hrefMatch.Success)
        {
            var currentPart = int.Parse(currentMatch.Groups[1].Value);
            var hrefPart = int.Parse(hrefMatch.Groups[1].Value);
            return hrefPart == currentPart + 1;
        }

        return false;
    }

    /// <summary>
    /// Resolves a relative URL and appends query parameters from the source.
    /// </summary>
    private static string ResolveUrlWithQueryParams(string href, string baseUrl, string queryString)
    {
        var resolved = ResolveUrl(href, baseUrl);

        // If the resolved URL doesn't have query params and we have some to add
        if (!string.IsNullOrEmpty(queryString) && !resolved.Contains('?'))
        {
            resolved += "?" + queryString;
        }
        else if (!string.IsNullOrEmpty(queryString) && resolved.Contains('?') && !resolved.Contains("UI="))
        {
            // URL has query params but not our UI/DB params
            resolved += "&" + queryString;
        }

        return resolved;
    }
}
