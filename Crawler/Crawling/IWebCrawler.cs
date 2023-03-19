﻿using System;
using Gemini.Net;
namespace Kennedy.Crawler.Crawling;

public interface IWebCrawler
{
    bool KeepWorkersAlive { get; }

    GeminiUrl GetUrl(int crawlerID = 0);

    

    void ProcessRequestResponse(GeminiResponse resp, Exception ex);
}