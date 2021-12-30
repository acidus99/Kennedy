using System;
using System.Linq;

using Gemi.Net;

namespace GemiCrawler.GemText
{
    /// <summary>
    /// Attempts to determine a "title" for a gemtext page
    /// </summary>
    public static class TitleFinder
    {
        public static string ExtractTitle(GemiResponse resp)
        {
            if (resp.IsSuccess && resp.HasBody && resp.MimeType.StartsWith("text/gemini"))
            {
                var t = resp.BodyText.Split("\n")
                    .Where(x => x.TrimStart().StartsWith("# ") && x.TrimStart().Length > 2)
                    .FirstOrDefault().Substring(2);
                return t;
            }

            return "";
        }
    }
}
