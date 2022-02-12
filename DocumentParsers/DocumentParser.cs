using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using NTextCat;

using Gemini.Net.Crawler.GemText;

namespace Gemini.Net.Crawler.DocumentParsers
{
    public class DocumentParser
    {
        //minimum size we require the content to be to find out the language
        const int MinSizeForLanguage = 150;

        RankedLanguageIdentifier langClassifier;

        public DocumentParser()
        {
            // Don't forget to deploy a language profile (e.g. Core14.profile.xml) with your application.
            // (take a look at "content" folder inside of NTextCat nupkg and here: https://github.com/ivanakcheurov/ntextcat/tree/master/src/LanguageModels).
            var factory = new RankedLanguageIdentifierFactory();
            langClassifier = factory.Load("Core14.profile.xml"); // can be an absolute or relative path. Beware of 260 chars limitation of the path length in Windows. Linux allows 4096 chars.
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
                Language = DetectLanguage(filteredBody)
            };
        }

        private int CountLines(string bodyText)
            => bodyText.Split("\n").Length;

        private string DetectLanguage(string filteredBody)
        {
            if (filteredBody.Length > MinSizeForLanguage)
            {
                var mostCertainLanguage = langClassifier.Identify(filteredBody).FirstOrDefault();
                return (mostCertainLanguage != null) ? mostCertainLanguage.Item1.Iso639_3 : "";
            }
            return "";
        }
    }
}
