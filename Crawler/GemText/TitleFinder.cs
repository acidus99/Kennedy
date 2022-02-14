﻿using System;
using System.Linq;

using Gemini.Net;

namespace Kennedy.Crawler.GemText
{
    /// <summary>
    /// Attempts to determine a "title" for a gemtext page
    /// </summary>
    public static class TitleFinder
    {
        public static string ExtractTitle(GeminiResponse resp)
        {
            if (resp.IsSuccess && resp.HasBody && resp.MimeType.StartsWith("text/gemini"))
            {
                var t = resp.BodyText.Split("\n")
                    .Where(x => x.StartsWith("# ") && x.Length > 2)
                    .FirstOrDefault();
                return t == null ? "" : t.Substring(2);
            }

            return "";
        }
    }
}