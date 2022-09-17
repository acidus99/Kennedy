


using Gemini.Net;
using Kennedy.Data.Models;
using Kennedy.Data.Parsers.GemText;

namespace Kennedy.Data.Parsers
{
    public class DocumentParser
    {
        LanguageDetector languageDetector;

        public DocumentParser(string crawlDataDirectory)
        {
            languageDetector = new LanguageDetector(crawlDataDirectory);
        }

        private bool IsGemText(GeminiResponse resp)
            => resp.HasBody && resp.IsSuccess && resp.MimeType.StartsWith("text/gemini");

        public DocumentMetadata ParseDocument(GeminiResponse resp)
        {
            if (resp.IsRedirect)
            {
                var link = LinkFinder.Create(resp.RequestUrl, resp.Meta);
                if (link != null)
                {
                    return new DocumentMetadata(link);
                }
            }

            if (IsGemText(resp))
            {
                return ParseGemText(resp);
            }
            return new DocumentMetadata();
        }

        private DocumentMetadata ParseGemText(GeminiResponse resp)
        {

            var filteredBody = LineParser.FilterBody(resp.BodyText);

            return new DocumentMetadata
            {
                FilteredBody = filteredBody,
                Links = LinkFinder.ExtractBodyLinks(resp.RequestUrl, resp.BodyText).ToList(),
                Title = TitleFinder.ExtractTitle(resp),
                LineCount = CountLines(resp.BodyText),
                Language = languageDetector.DetectLanguage(filteredBody)
            };
        }

        private int CountLines(string bodyText)
            => bodyText.Split("\n").Length;
    }
}
