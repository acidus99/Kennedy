using Gemini.Net;

namespace Kennedy.Data.Parsers;

/// <summary>
/// Entry point for the response parsing pipeline.
/// Dispatches a raw <see cref="GeminiResponse"/> to the appropriate format-specific parser
/// and returns an enriched <see cref="ParsedResponse"/> suitable for storage.
///
/// Pipeline order:
/// 1. Redirect short-circuit (extracts redirect target as a <see cref="FoundLink"/>)
/// 2. Non-success / no-body short-circuit (bare ParsedResponse)
/// 3. <see cref="BinaryParser"/> — magic-byte detection for images and binary blobs
/// 4. <see cref="TextParser"/> — MIME + content sniffing for Gemtext and plain text
/// 5. Fallback to binary ParsedResponse
/// </summary>
public class ResponseParser
{
    BinaryParser binaryParser;
    TextParser textParser;

    public ResponseParser()
    {
        binaryParser = new BinaryParser();
        textParser = new TextParser();
    }

    /// <summary>
    /// Convenience overload that parses raw WARC response bytes before calling <see cref="Parse(GeminiResponse)"/>.
    /// </summary>
    public ParsedResponse Parse(GeminiUrl url, byte[] completeResponse)
    {
        GeminiResponse resp = GeminiParser.ParseResponseBytes(url, completeResponse);
        return Parse(resp);
    }

    /// <summary>
    /// Runs the full parsing pipeline on an already-decoded <see cref="GeminiResponse"/>.
    /// Always returns a non-null result; uses the base <see cref="ParsedResponse"/> for unrecognized formats.
    /// </summary>
    public ParsedResponse Parse(GeminiResponse resp)
    {
        ParsedResponse? parsedResponse = TryParseRedirect(resp);
        if(parsedResponse != null)
        {
            return parsedResponse;
        }

        if (!resp.IsSuccess || !resp.HasBody)
        {
            //unknown response
            return new ParsedResponse(resp);
        }

        //check for known binary formats
        parsedResponse = binaryParser.Parse(resp);

        if(parsedResponse != null)
        {
            return parsedResponse;
        }

        //check for text formats
        parsedResponse = textParser.Parse(resp);

        if(parsedResponse != null)
        {
            return parsedResponse;
        }

        //fail back on binary
        return new ParsedResponse(resp)
        {
            FormatType = ContentType.Binary
        };
    }

    /// <summary>
    /// If the response is a redirect, wraps the Meta field (the redirect target URL) as a FoundLink.
    /// This ensures redirect targets are added to the URL registry even when the body is empty.
    /// </summary>
    private ParsedResponse? TryParseRedirect(GeminiResponse resp)
    {
        if (resp.IsRedirect)
        {
            var link = FoundLink.Create(resp.RequestUrl, resp.Meta);
            if (link != null)
            {
                return new ParsedResponse(resp)
                {
                    Links = { link }
                };
            }
        }
        return null;
    }
}