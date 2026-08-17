using System.Text.Encodings.Web;
using System.Text.Json;

namespace DoubanAwardsCollector;

internal static class JsonDefaults
{
    public static JsonSerializerOptions Read { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static JsonSerializerOptions Write { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };
}
