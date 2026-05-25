using Gemini.Net;

namespace Kennedy.Data;

/// <summary>
/// A hyperlink discovered in a Gemini document, resolved to an absolute <see cref="GeminiUrl"/>.
/// Equality is URL-only — duplicate links to the same URL on one page are collapsed.
/// </summary>
public class FoundLink : IEquatable<FoundLink>
{
    /// <summary>The resolved absolute Gemini URL this link points to.</summary>
    public required GeminiUrl Url { get; init; }

    /// <summary>True when the target URL has a different authority (host:port) than the source page.</summary>
    public required bool IsExternal { get; init; }

    /// <summary>Human-readable label from the link line. Empty string when no label is present.</summary>
    public required string LinkText { get; init; }

    /// <summary>
    /// What makes a FoundLink unique is really just its URL.
    /// </summary>
    public bool Equals(FoundLink? other)
        => other != null && Url.Equals(other.Url);

    public override bool Equals(object? obj)
        => Equals(obj as GeminiUrl);

    public override int GetHashCode()
        => Url.GetHashCode();

    /// <summary>
    /// Resolves <paramref name="foundUrl"/> relative to <paramref name="pageUrl"/> and constructs a FoundLink.
    /// Returns null if the URL cannot be resolved or is not a gemini:// URL.
    /// </summary>
    public static FoundLink? Create(GeminiUrl pageUrl, string foundUrl, string linkText = "")
    {
        var newUrl = GeminiUrl.MakeUrl(pageUrl, foundUrl);
        //ignore anything that doesn't resolve properly, or isn't to a gemini:// URL
        if (newUrl == null)
        {
            return null;
        }
        return new FoundLink
        {
            Url = newUrl,
            IsExternal = (newUrl.Authority != pageUrl.Authority),
            LinkText = linkText
        };
    }
}