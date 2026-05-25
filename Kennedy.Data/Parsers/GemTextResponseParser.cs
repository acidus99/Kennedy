using System.Text;
using System.Text.RegularExpressions;
using Gemini.Net;
using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers;

/// <summary>
/// Parses <c>text/gemini</c> responses into <see cref="GemTextResponse"/> objects.
/// Orchestrates all Gemtext-specific sub-parsers: link extraction, title detection,
/// hashtag/mention extraction, language detection, and Gemfeed identification.
/// </summary>
public class GemTextResponseParser : AbstractTextParser
{
    // Matches the date prefix used in Gemfeed link lines, e.g. "2024-03-15 My Post Title"
    static readonly Regex Iso8601Date = new Regex(@"^\d{4}\-[01]\d\-[0123]\d", RegexOptions.Compiled);

    LanguageDetector languageDetector = new LanguageDetector();

    /// <summary>Handles any response that is textual and declares a text/gemini MIME type.</summary>
    public override bool CanParse(GeminiResponse resp, bool isTextBody)
        => isTextBody && resp.MimeType!.StartsWith("text/gemini");

    /// <summary>
    /// Full parse pipeline for a Gemtext body:
    /// 1. Split into lines and strip preformatted blocks (``` fences).
    /// 2. Build indexable text — link lines contribute only their label, not the URL.
    /// 3. Extract links (gemini:// and relative only).
    /// 4. Detect title, hashtags, mentions, language, and feed status.
    /// </summary>
    public override ParsedResponse? Parse(GeminiResponse resp)
    {
        string[] lines = LineParser.GetLines(resp.BodyText);

        // Preformatted blocks contain ASCII art / code — stripping them keeps the FTS index clean
        // and prevents spurious keyword matches inside raw code listings.
        List<string> noPreformatted = LineParser.RemovePreformattedLines(lines);
        var indexableText = GetIndexableContent(noPreformatted);

        // Links are extracted from non-preformatted lines only; non-gemini links are filtered out.
        List<FoundLink> links = LinkFinder.GetLinks(resp.RequestUrl, noPreformatted).ToList();

        return new GemTextResponse(resp)
        {
            DetectedLanguage = languageDetector.DetectLanguage(indexableText),
            IndexableText = indexableText,
            IsFeed = IsGemFeed(links),
            LineCount = lines.Length,
            Links = links,
            Title = TitleFinder.FindTitle(lines),

            HashTags = HashtagsFinder.GetHashtags(noPreformatted),
            Mentions = MentionsFinder.GetMentions(noPreformatted)
        };
    }

    /// <summary>
    /// Builds the string that will be stored in Documents.Content and indexed by FTS5.
    /// For link lines, only the human-readable label is included (not the raw URL),
    /// which keeps search results relevant to the actual text a human would read.
    /// </summary>
    private string GetIndexableContent(IEnumerable<string> noPreformatted)
    {
        var sb = new StringBuilder();
        foreach (string line in noPreformatted)
        {
            if (line.StartsWith("=>"))
            {
                sb.AppendLine(LinkFinder.GetLinkText(line));
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Checks found links for the gemfeed format. If more than 2 are found, consider this a feed
    /// </summary>
    /// <param name="links"></param>
    /// <returns></returns>
    private bool IsGemFeed(List<FoundLink> links)
        => links.Where(x => x.LinkText.Length >= 10 && Iso8601Date.IsMatch(x.LinkText)).Count() >= 2;

}