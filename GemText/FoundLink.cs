using System;
using Gemini.Net;
namespace Gemini.Net.Crawler.GemText
{
    public class FoundLink : IEquatable<FoundLink>
    {
        public GeminiUrl Url { get; set; }
        public bool IsExternal { get; set; }
        public string LinkText { get; set; }

        /// <summary>
        /// What makes a FoundLink unique is really just its URL. We are properly ignoring links to the same thing with different LinkText
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(FoundLink other)
            => other != null && Url.Equals(other.Url);

        public override bool Equals(object obj)
            => Equals(obj as GeminiUrl);

        public override int GetHashCode()
            => Url.GetHashCode();

    }
}
