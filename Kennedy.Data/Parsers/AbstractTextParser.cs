using Gemini.Net;

namespace Kennedy.Data.Parsers;

/// <summary>
/// Base class for format-specific text parsers.
/// <see cref="TextParser"/> iterates registered implementations in order,
/// calling <see cref="CanParse"/> to find the first that claims the response,
/// then <see cref="Parse"/> to produce the enriched result.
/// </summary>
public abstract class AbstractTextParser
{
    /// <summary>
    /// Returns true when this parser can handle the given response.
    /// <paramref name="isTextBody"/> is pre-computed by <see cref="MimeSniffer"/> to avoid
    /// each parser having to repeat the binary-byte scan.
    /// </summary>
    public abstract bool CanParse(GeminiResponse resp, bool isTextBody);

    /// <summary>
    /// Parses the response into an enriched <see cref="ParsedResponse"/>.
    /// Returns null only on catastrophic failure; callers fall through to the next parser.
    /// </summary>
    public abstract ParsedResponse? Parse(GeminiResponse resp);
}