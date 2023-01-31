using System;
using Gemini.Net;
namespace Kennedy.Blazer.Crawling;

public interface ICrawler
{
    bool KeepWorkersAlive { get; }

    GeminiUrl GetUrl(int crawlerID = 0);

    

    void ProcessRequestResponse(GeminiResponse resp, Exception ex);
}
