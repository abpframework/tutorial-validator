using System.ComponentModel;
using System.Net;
using System.Text;
using Microsoft.SemanticKernel;

namespace Validator.Executor.Plugins;

/// <summary>
/// Plugin for making HTTP requests.
/// Used by the AI agent to test HTTP endpoints and verify responses.
/// </summary>
public class HttpPlugin : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _defaultTimeout;

    /// <summary>
    /// Creates a new HttpPlugin with default settings.
    /// </summary>
    /// <param name="timeout">Request timeout. Defaults to 30 seconds.</param>
    public HttpPlugin(TimeSpan? timeout = null)
    {
        _defaultTimeout = timeout ?? TimeSpan.FromSeconds(30);
        
        var handler = new HttpClientHandler
        {
            // Allow self-signed certificates for localhost/loopback testing only
            ServerCertificateCustomValidationCallback = (message, _, _, sslPolicyErrors) =>
            {
                if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
                    return true;

                // Only bypass certificate errors for localhost/loopback addresses
                var host = message.RequestUri?.Host;
                if (host is "localhost" or "127.0.0.1" or "::1" ||
                    (host != null && host.StartsWith("localhost:")))
                    return true;

                return false;
            }
        };
        
        _httpClient = new HttpClient(handler)
        {
            Timeout = _defaultTimeout
        };
    }

    /// <summary>
    /// Makes an HTTP GET request to the specified URL.
    /// </summary>
    [KernelFunction]
    [Description("Make an HTTP GET request to a URL. Returns status code and response body.")]
    public async Task<string> GetAsync(
        [Description("The URL to request (e.g., 'https://localhost:44321')")] string url)
    {
        return await MakeRequestAsync(HttpMethod.Get, url, null);
    }

    /// <summary>
    /// Makes an HTTP POST request to the specified URL.
    /// </summary>
    [KernelFunction]
    [Description("Make an HTTP POST request to a URL with optional JSON body. Returns status code and response body.")]
    public async Task<string> PostAsync(
        [Description("The URL to request")] string url,
        [Description("Optional JSON body content")] string? jsonBody = null)
    {
        return await MakeRequestAsync(HttpMethod.Post, url, jsonBody);
    }

    /// <summary>
    /// Makes an HTTP request with the specified method.
    /// </summary>
    [KernelFunction]
    [Description("Make an HTTP request with any method (GET, POST, PUT, DELETE, etc.). Returns status code and response body.")]
    public async Task<string> RequestAsync(
        [Description("The HTTP method (GET, POST, PUT, DELETE, PATCH, etc.)")] string method,
        [Description("The URL to request")] string url,
        [Description("Optional JSON body content for POST/PUT/PATCH requests")] string? jsonBody = null)
    {
        var httpMethod = new HttpMethod(method.ToUpperInvariant());
        return await MakeRequestAsync(httpMethod, url, jsonBody);
    }

    private async Task<string> MakeRequestAsync(HttpMethod method, string url, string? jsonBody)
    {
        Console.WriteLine($"    [HTTP] {method} {url}");

        try
        {
            using var request = new HttpRequestMessage(method, url);
            
            if (!string.IsNullOrEmpty(jsonBody))
            {
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            using var response = await _httpClient.SendAsync(request);
            
            var statusCode = (int)response.StatusCode;
            var statusName = response.StatusCode.ToString();
            var body = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"           Status: {statusCode} {statusName}");

            var sb = new StringBuilder();
            sb.AppendLine($"HTTP Response:");
            sb.AppendLine($"  Status: {statusCode} {statusName}");
            sb.AppendLine($"  Content-Type: {response.Content.Headers.ContentType}");
            sb.AppendLine($"  Content-Length: {body.Length} characters");
            sb.AppendLine();
            
            // Truncate very long responses
            if (body.Length > 5000)
            {
                sb.AppendLine("Response Body (truncated to 5000 chars):");
                sb.AppendLine(body[..5000]);
                sb.AppendLine("... [truncated]");
            }
            else
            {
                sb.AppendLine("Response Body:");
                sb.AppendLine(body);
            }

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"           [ERROR] {ex.Message}");
            return $"HTTP ERROR: {ex.Message}\nThis could mean the server is not running or the URL is incorrect.";
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"           [ERROR] Request timed out");
            return $"HTTP ERROR: Request timed out after {_defaultTimeout.TotalSeconds} seconds.\nThis could mean the server is not responding.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"           [ERROR] {ex.Message}");
            return $"HTTP ERROR: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks if a URL is reachable (returns any successful status code).
    /// </summary>
    [KernelFunction]
    [Description("Check if a URL is reachable. Makes a GET request and returns whether the server responded.")]
    public async Task<string> IsReachableAsync(
        [Description("The URL to check")] string url)
    {
        Console.WriteLine($"    [HTTP] Checking if reachable: {url}");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await _httpClient.SendAsync(request);
            
            var statusCode = (int)response.StatusCode;
            var isSuccess = response.IsSuccessStatusCode;
            
            Console.WriteLine($"           Reachable: {isSuccess} (Status: {statusCode})");

            if (isSuccess)
            {
                return $"REACHABLE: {url} responded with status {statusCode}";
            }
            else
            {
                return $"REACHABLE but returned error: {url} responded with status {statusCode} {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"           Not reachable: {ex.Message}");
            return $"NOT REACHABLE: {url} - {ex.Message}";
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
