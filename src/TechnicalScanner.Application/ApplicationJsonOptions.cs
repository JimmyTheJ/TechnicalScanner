using System.Text.Json;

namespace TechnicalScanner.Application;

internal static class ApplicationJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
