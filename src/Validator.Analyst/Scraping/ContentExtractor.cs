using AngleSharp.Dom;

namespace Validator.Analyst.Scraping;

/// <summary>
/// Extracts and cleans content from HTML documents.
/// Finds the main content area, extracts titles and images, and removes noise elements.
/// </summary>
internal static class ContentExtractor
{
    /// <summary>
    /// Extracts the page title from the document.
    /// </summary>
    internal static string ExtractTitle(IDocument document)
    {
        // Try to find the main heading first
        var h1 = document.QuerySelector("article h1, .content h1, main h1, h1");
        if (h1 != null)
        {
            return h1.TextContent.Trim();
        }

        // Fall back to document title
        return document.Title?.Trim() ?? "Untitled";
    }

    /// <summary>
    /// Finds the main content area element in the document.
    /// </summary>
    internal static IElement? FindContentArea(IDocument document)
    {
        // ABP docs typically use article, .content, or main
        return document.QuerySelector("article, .content, main, .document-content, .docs-content")
               ?? document.Body;
    }

    /// <summary>
    /// Extracts image URLs from the content area.
    /// </summary>
    internal static List<string> ExtractImageUrls(IElement? contentArea, string sourceUrl)
    {
        var imageUrls = new List<string>();

        if (contentArea == null)
        {
            return imageUrls;
        }

        var images = contentArea.QuerySelectorAll("img");
        foreach (var img in images)
        {
            var src = img.GetAttribute("src");
            if (!string.IsNullOrEmpty(src) && !src.Contains("noimg-user"))
            {
                var absoluteUrl = NavigationExtractor.ResolveUrl(src, sourceUrl);
                imageUrls.Add(absoluteUrl);
            }
        }

        return imageUrls;
    }

    /// <summary>
    /// Removes noise elements from the content area (navigation, feedback, social links, etc.).
    /// </summary>
    internal static void CleanContentArea(IElement? contentArea)
    {
        if (contentArea == null)
        {
            return;
        }

        // Remove elements that shouldn't be in the content
        var selectorsToRemove = new[]
        {
            // Generic navigation/structure elements
            "nav", "header", "footer", "aside", "script", "style", "noscript",
            ".nav", ".sidebar", ".menu", ".footer", ".header", ".toc", ".breadcrumb",
            ".navigation", ".pagination", ".edit-link", ".feedback", ".share-buttons",
            
            // ABP docs specific elements
            ".document-options",           // UI/DB dropdown selector
            ".contributors",               // Contributors section
            ".doc-feedback",               // Feedback form
            ".translate-options",          // Translate dropdown
            ".social-share",               // Share buttons
            ".btn-group",                  // Button groups (often UI/DB switchers)
            
            // Attribute selectors for ABP docs
            "[class*='feedback']",         // Any feedback elements
            "[class*='contributor']",      // Any contributor elements
            "[class*='translate']",        // Translation elements
            "[class*='social']",           // Social share elements
            "[class*='edit-page']",        // Edit page links
            "[class*='document-option']",  // Document options
            
            // Common noise elements
            ".was-helpful",                // "Was this page helpful?" section
            ".page-feedback",              // Page feedback form
            ".last-updated",               // Last updated info
            ".edit-github",                // Edit on GitHub links
        };

        foreach (var selector in selectorsToRemove)
        {
            try
            {
                var elements = contentArea.QuerySelectorAll(selector);
                foreach (var element in elements)
                {
                    element.Remove();
                }
            }
            catch
            {
                // Some selectors might not be valid, skip them
            }
        }

        // Remove elements containing specific text patterns
        RemoveElementsContainingText(contentArea, "Was this page helpful?");
        RemoveElementsContainingText(contentArea, "Contributors");
        RemoveElementsContainingText(contentArea, "Edit this page on GitHub");
        RemoveElementsContainingText(contentArea, "Submit Your Feedback");
        RemoveElementsContainingText(contentArea, "Thank you for your valuable feedback");
        RemoveElementsContainingText(contentArea, "Share on X");
        RemoveElementsContainingText(contentArea, "Undo Translation");
        RemoveElementsContainingText(contentArea, "Document Options");
    }

    private static void RemoveElementsContainingText(IElement contentArea, string text)
    {
        // Find divs/sections containing specific text and remove them
        var candidates = contentArea.QuerySelectorAll("div, section, aside, p, h3");
        foreach (var element in candidates)
        {
            if (element.TextContent.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                // Only remove if it's a container element, not the main content
                var parent = element.ParentElement;
                if (parent != null && parent != contentArea)
                {
                    // Check if this is a small section, not the main content
                    if (element.TextContent.Length < 500)
                    {
                        element.Remove();
                    }
                }
            }
        }
    }
}
