using System.Text.Json;
using DoubanAwardsCollector.Models;

namespace DoubanAwardsCollector;

internal static class AwardJsonStore
{
    public static async Task<string> SaveAsync(
        AwardEditionData document,
        CancellationToken cancellationToken)
    {
        AppPaths.EnsureCreated();

        var safeSlug = Sanitize(document.Event.Slug);
        var safeEdition = Sanitize(document.Edition.Key);
        var path = Path.Combine(AppPaths.JsonFolder, $"{safeSlug}-{safeEdition}.json");

        var json = JsonSerializer.Serialize(document, JsonDefaults.Write);
        await File.WriteAllTextAsync(path, json, cancellationToken);
        return path;
    }

    private static string Sanitize(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }
}
