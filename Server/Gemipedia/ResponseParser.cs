using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace Kennedy.Gemipedia
{
	public static class ResponseParser
	{
        public static List<ArticleSummary> ParseSearchResponse(string json)
        {
            var response = ParseJson(json);
            List<ArticleSummary> ret = new List<ArticleSummary>();
            foreach (JObject result in ((JArray) response["pages"]))
            {
                ret.Add(new ArticleSummary
                {
                    Title = Cleanse(result["title"] as JToken),
                    Excerpt = StripHtml(Cleanse(result["excerpt"])),
                    Description = Cleanse(result["description"]),
                    ThumbnailUrl = GetThumbnailUrl(result["thumbnail"] as JObject)
                });
            }
            return ret;
        }

        private static string GetThumbnailUrl(JObject thumb)
        {
            //result["thumbnail"]?["url"]? doesn't seem to work
            if (thumb != null)
            {
                var url = thumb["url"]?.ToString() ??
                            thumb["source"]?.ToString() ?? "";
                if (url.Length > 0)
                {
                    return EnsureHttps(url);
                }
            }
            return "";
        }

        private static string EnsureHttps(string url)
            => url.StartsWith("https:") ? url : "https:" + url;

        private static string Cleanse(JToken? token)
            => token?.ToString() ?? "";

        private static JObject ParseJson(string json)
            => JObject.Parse(json);

        private static string StripHtml(string s)
            => WebUtility.HtmlDecode(Regex.Replace(s, @"<[^>]*>", "")) + "...";
    }
}

