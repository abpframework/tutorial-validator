using Validator.Analyst.Scraping.Models;

namespace Validator.Analyst.Scraping;

/// <summary>
/// Abstraction for parsing HTML content into markdown.
/// </summary>
public interface IHtmlParser
{
    /// <summary>
    /// Parses HTML content and converts it to markdown.
    /// </summary>
    /// <param name="html">The raw HTML content.</param>
    /// <param name="sourceUrl">The URL the HTML was fetched from (for resolving relative URLs).</param>
    /// <returns>A parsed page result containing markdown content and navigation info.</returns>
    ParsedPageResult Parse(string html, string sourceUrl);
}
