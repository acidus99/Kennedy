using System;
using Gemini.Net;
namespace Gemini.Net.Crawler
{
    public interface ICrawler
    {

        bool KeepWorkersAlive { get; }

        GeminiUrl GetNextUrl(int crawlerID = 0);

        void ProcessResult(GeminiUrl url, GeminiResponse resp, Exception ex);
    }
}
