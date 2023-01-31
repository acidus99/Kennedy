using System;
using Gemini.Net;
using Kennedy.Blazer.Logging;

namespace Kennedy.Blazer.Frontiers
{
    public interface IUrlFrontier : IStatusProvider
    {
        /// <summary>
        /// How many URLs are in the frontier
        /// </summary>
        int Count { get; }

        int Total { get; }

        /// <summary>
        /// Adds a URL to the frontier
        /// </summary>
        /// <param name="url"></param>
        void AddUrl(GeminiUrl url);

        /// <summary>
        /// Gets the next URL from the frontier
        /// </summary>
        /// <returns></returns>
        GeminiUrl GetUrl(int crawlerID);

    }
}