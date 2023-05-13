using System;

using Gemini.Net;

namespace Kennedy.Data
{
    public class FoundLink : IEquatable<FoundLink>
    {
        public required GeminiUrl Url { get; set; }
        public bool IsExternal { get; set; }
        public string LinkText { get; set; }

        /// <summary>
        /// What makes a FoundLink unique is really just its URL.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(FoundLink other)
            => other != null && Url.Equals(other.Url);

        public override bool Equals(object obj)
            => Equals(obj as GeminiUrl);

        public override int GetHashCode()
            => Url.GetHashCode();

        public static FoundLink Create(GeminiUrl pageUrl, string foundUrl, string linkText = "")
        {
            var newUrl = GeminiUrl.MakeUrl(pageUrl, foundUrl);
            //ignore anything that doesn't resolve properly, or isn't to a gemini:// URL
            if (newUrl == null)
            {
                return null;
            }
            return new FoundLink
            {
                Url = newUrl,
                IsExternal = (newUrl.Authority != pageUrl.Authority),
                LinkText = linkText
            };
        }
    }
}
