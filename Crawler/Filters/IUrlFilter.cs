using System;
using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.Crawler.Filters
{
    /// <summary>
    /// Abstract module that determines if a URL is allowed to be added to the Url Frontier
    /// </summary>
    public interface IUrlFilter
    {
        public abstract BlockResult IsUrlAllowed(UrlFrontierEntry entry);
    }
}
