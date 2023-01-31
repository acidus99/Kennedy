using System;
using Gemini.Net;

namespace Kennedy.Crawler.Frontiers
{
    /// <summary>
    /// Abstract module that determines if a URL is allowed to be added to the Url Frontier
    /// </summary>
    public interface IUrlFilter
    {
        public abstract bool IsUrlAllowed(GeminiUrl url);
    }
}
