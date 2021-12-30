using System;
using Gemi.Net;
namespace GemiCrawler.Links
{
    public class FoundLink : IEquatable<FoundLink>
    {
        public GemiUrl Url { get; set; }
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
            => Equals(obj as GemiUrl);

        public override int GetHashCode()
            => Url.GetHashCode();

    }
}
