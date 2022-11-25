using System;
using Gemini.Net;

namespace Kennedy.Blazer.Frontiers
{
    public interface IUrlFrontier
    {
        /// <summary>
        /// How many URLs are in the frontier
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Adds a URL to the frontier
        /// </summary>
        /// <param name="url"></param>
        void AddUrl(GeminiUrl url);

        /// <summary>
        /// Gets the next URL from the frontier
        /// </summary>
        /// <returns></returns>
        GeminiUrl GetUrl();

    }
}