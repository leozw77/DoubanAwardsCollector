using System.Text.RegularExpressions;

namespace DoubanAwardsCollector;

internal sealed record AwardUrl(
    string Slug,
    string EditionKey,
    Uri NormalizedUri)
{
    private static readonly Regex RouteRegex = new(
        @"^/awards/(?<slug>[^/]+)/(?<edition>[^/]+)(?:/.*)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool TryNormalize(string input, out AwardUrl? result, out string error)
    {
        result = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "链接为空。";
            return false;
        }

        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
        {
            error = "不是有效的绝对 URL。";
            return false;
        }

        if (!string.Equals(uri.Host, "movie.douban.com", StringComparison.OrdinalIgnoreCase))
        {
            error = "只接受 movie.douban.com。";
            return false;
        }

        var match = RouteRegex.Match(uri.AbsolutePath);
        if (!match.Success)
        {
            error = "不是 /awards/{slug}/{edition}/... 格式。";
            return false;
        }

        var slug = Uri.UnescapeDataString(match.Groups["slug"].Value);
        var edition = Uri.UnescapeDataString(match.Groups["edition"].Value);

        var builder = new UriBuilder(Uri.UriSchemeHttps, "movie.douban.com")
        {
            Path = $"/awards/{Uri.EscapeDataString(slug)}/{Uri.EscapeDataString(edition)}/nominees",
            Query = "k=a"
        };

        result = new AwardUrl(slug, edition, builder.Uri);
        return true;
    }
}
