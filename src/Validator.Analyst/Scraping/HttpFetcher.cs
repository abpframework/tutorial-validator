using System.Net;

namespace Validator.Analyst.Scraping;

/// <summary>
/// HTTP fetcher implementation using HttpClient.
/// </summary>
public class HttpFetcher : IHttpFetcher
{
    private readonly HttpClient _httpClient;
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;

    /// <summary>
    /// Creates a new HttpFetcher with default settings.
    /// </summary>
    public HttpFetcher() : this(new HttpClient(), maxRetries: 3, initialDelay: TimeSpan.FromSeconds(1))
    {
    }

    /// <summary>
    /// Creates a new HttpFetcher with custom HttpClient and retry settings.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use.</param>
    /// <param name="maxRetries">Maximum number of retry attempts for transient failures.</param>
    /// <param name="initialDelay">Initial delay before first retry (doubles each attempt).</param>
    public HttpFetcher(HttpClient httpClient, int maxRetries = 3, TimeSpan? initialDelay = null)
    {
        _httpClient = httpClient;
        _maxRetries = maxRetries;
        _initialDelay = initialDelay ?? TimeSpan.FromSeconds(1);

        // Set default headers if not already configured
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TutorialValidator/1.0");
        }
    }

    /// <inheritdoc/>
    public async Task<string> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ScrapingException($"Invalid URL format: {url}", url);
        }

        Exception? lastException = null;
        var delay = _initialDelay;

        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2; // Exponential backoff
                }

                using var response = await _httpClient.GetAsync(uri, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync(cancellationToken);
                }

                // Don't retry client errors (4xx) except rate limiting
                if (response.StatusCode >= HttpStatusCode.BadRequest &&
                    response.StatusCode < HttpStatusCode.InternalServerError &&
                    response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    throw new ScrapingException(
                        $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {url}",
                        url,
                        (int)response.StatusCode);
                }

                lastException = new ScrapingException(
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {url}",
                    url,
                    (int)response.StatusCode);
            }
            catch (HttpRequestException ex)
            {
                lastException = new ScrapingException(
                    $"Network error fetching {url}: {ex.Message}",
                    url,
                    innerException: ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = new ScrapingException(
                    $"Request timeout for {url}",
                    url,
                    innerException: ex);
            }
            catch (ScrapingException)
            {
                throw; // Re-throw non-retryable scraping exceptions
            }
        }

        throw lastException ?? new ScrapingException($"Failed to fetch {url} after {_maxRetries} retries", url);
    }
}
