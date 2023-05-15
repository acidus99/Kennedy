using System;
using System.Linq;
using System.Text.RegularExpressions;

using Gemini.Net;

namespace Kennedy.Data.Parsers.GemText
{
    /// <summary>
    /// Attempts to determine a "title" for a gemtext page
    /// Rules:
    /// - Look for any header
    /// - Look the first preformatted text section for an alt text (used for
    ///     ascii art logos)
    /// </summary>
    public static class TitleFinder
    {
        static readonly Regex headingRegex = new Regex(@"^(#+)\s*(.+)", RegexOptions.Compiled);

        public static string? ExtractTitle(GeminiResponse resp)
        {
            if (resp.IsSuccess && resp.HasBody && resp.MimeType != null && resp.MimeType.StartsWith("text/gemini"))
            {
                return ExtractTitle(resp.BodyText);
            }
            return null;
        }

        public static string? ExtractTitle(string gemText)
        {
            var title = TryHeaders(gemText);
            if(title?.Length > 0)
            {
                return title;
            }
            return TryPreformatted(gemText);
        }

        private static string? TryHeaders(string gemText)
        {
            var t = gemText.Split("\n")
                   .Where(x => x.StartsWith("#") && x.Length > 2)
                   .FirstOrDefault();
            if (t != null)
            {
                var match = headingRegex.Match(t);
                if (match.Success)
                {
                    t = match.Groups[2].Value.Trim();
                }
            }
            return t;
        }

        private static string TryPreformatted(string gemText)
        {
            var t = gemText.Split("\n")
                   .Where(x => x.StartsWith("```"))
                   .FirstOrDefault() ?? "";

            return (t.Length > 3) ? t.Substring(3).Trim() : "";
        }
    }
}
