using System.Text.Json;
using System.Text.Json.Serialization;

namespace Validator.Core;

/// <summary>
/// Provides consistent JSON serialization options for the test plan.
/// </summary>
public static class JsonSerializerOptionsProvider
{
    private static JsonSerializerOptions? _default;

    /// <summary>
    /// Default JSON serializer options with camelCase naming and pretty printing.
    /// </summary>
    public static JsonSerializerOptions Default => _default ??= CreateDefaultOptions();

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)
            }
        };
    }
}
