using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Gemini.Net;

namespace Kennedy.SearchIndex.Search
{
	internal class PathTokenizer
	{
        static char[] splitTokens = { '-', '_', '.', ',', };

        List<string> tokens = new List<string>();

        public string[] GetTokens(string url)
        {
            return GetTokens(new GeminiUrl(url));
        }

        public string[] GetTokens(GeminiUrl gurl)
        {
            tokens.Clear();

            foreach (string rawSegment in GetSegments(gurl))
            {
                var token = CleanToken(rawSegment);
                if (token.Length > 0)
                {
                    foreach (var subToken in SplitOnBreaks(token))
                    {
                        ProcessToken(subToken);
                    }
                }
            }
            return tokens.ToArray();
        }

        private string CleanToken(string t)
        {
            t = WebUtility.UrlDecode(t).Trim();

            t = t.Replace("/", "");
            t = t.Replace(".gif", "");
            t = t.Replace(".jpg", "");
            t = t.Replace(".jpeg", "");
            t = t.Replace(".png", "");
            t = t.Replace(".webp", "");
            return t;
        }

        private IEnumerable<string> GetSegments(GeminiUrl url)
            => url.Url.Segments.Reverse();

        /// <summary>
        /// Takes a token, breaks out any CamelCase or pascalCase subtokens, and adds them all to the list of tokens
        /// </summary>
        /// <param name="token"></param>
        private void ProcessToken(string token)
        {
            if(IsAllUpper(token))
            {
                tokens.Add(token);
                return;
            }

            int start = 0;
            for (int curr = 1, len = token.Length; curr < len; curr++)
            {
                if (char.IsUpper(token[curr]))
                {
                    tokens.Add(token.Substring(start, curr - start));
                    start = curr;
                }
            }
            tokens.Add(token.Substring(start));
        }

        private bool IsAllUpper(string token)
        {
            foreach(char c in token)
            {
                //we include digits here a string like "AUTO5" would be broken in to "A U T O 5"
                if(!char.IsUpper(c) || !char.IsDigit(c))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Split the token into subtokens, on characters that are breaks (e.g. '.' '-' etc)
        /// </summary>
        private string[] SplitOnBreaks(string token)
            => token.Split(splitTokens, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

