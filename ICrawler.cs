using System;
using Gemini.Net;
namespace Gemini.Net.Crawler
{
    public interface ICrawler
    {

        bool KeepWorkersAlive { get; }

        GemiUrl GetNextUrl(int crawlerID = 0);

        void ProcessResult(GemiUrl url, GemiResponse resp, Exception ex);
    }
}
