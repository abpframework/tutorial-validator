namespace Validator.Analyst.Analysis.PromptTemplates;

/// <summary>
/// User prompt templates for tutorial analysis.
/// </summary>
public static class UserPrompts
{
    /// <summary>
    /// Creates a prompt for analyzing a single tutorial page.
    /// </summary>
    /// <param name="pageTitle">Title of the current page</param>
    /// <param name="pageIndex">Zero-based index of the page</param>
    /// <param name="totalPages">Total number of pages in the tutorial</param>
    /// <param name="ui">UI framework (mvc, blazor, angular)</param>
    /// <param name="database">Database type (ef, mongodb)</param>
    /// <param name="content">Markdown content of the page</param>
    /// <param name="startingStepId">The step ID to start numbering from</param>
    /// <returns>Formatted user prompt</returns>
    public static string AnalyzePage(
        string pageTitle,
        int pageIndex,
        int totalPages,
        string ui,
        string database,
        string content,
        int startingStepId)
    {
        return $"""
            ## Tutorial Context
            - Page: {pageIndex + 1} of {totalPages}
            - Title: {pageTitle}
            - UI Framework: {ui}
            - Database: {database}
            - Starting Step ID: {startingStepId}

            ## Instructions
            Analyze the following tutorial page content and extract all actionable steps.
            Number steps starting from {startingStepId}.
            Return ONLY a JSON array of steps, no other text.

            ## Tutorial Content
            {content}

            ## Response
            Return a JSON array of steps following the schema provided in the system prompt.
            If there are no actionable steps on this page, return an empty array: []
            """;
    }
}
