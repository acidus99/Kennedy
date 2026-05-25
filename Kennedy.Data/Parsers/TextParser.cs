using Gemini.Net;

namespace Kennedy.Data.Parsers;

/// <summary>
/// Dispatches text-based responses to the appropriate <see cref="AbstractTextParser"/> implementation.
/// Uses <see cref="MimeSniffer"/> once to determine whether the body bytes look like text,
/// then passes that result to each parser's <see cref="AbstractTextParser.CanParse"/> check.
/// Parsers are tried in registration order: Gemtext first, plain text second.
/// </summary>
public class TextParser
{
    MimeSniffer sniffer = new MimeSniffer();
    List<AbstractTextParser> parsers = new List<AbstractTextParser>
    {
        new GemTextResponseParser(),
        new PlainTextResponseParser(),
    };

    /// <summary>
    /// Attempts to parse <paramref name="resp"/> with each registered text parser.
    /// Returns the first non-null result, or null if no parser claims the response.
    /// </summary>
    public ParsedResponse? Parse(GeminiResponse resp)
    {
        if (resp.BodyBytes == null)
        {
            throw new ArgumentNullException(nameof(resp), "Response BodyBytes cannot be null");
        }

        bool isTextBody = sniffer.IsText(resp.BodyBytes);

        foreach (var parser in parsers)
        {
            if (parser.CanParse(resp, isTextBody))
            {
                var doc = parser.Parse(resp);
                if (doc != null)
                {
                    return doc;
                }
            }
        }

        return null;
    }
}