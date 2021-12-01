using System;
using Gemi.Net;
namespace GemiCrawler
{
    public interface ICrawler
    {

        bool KeepWorkersAlive { get; }

        GemiUrl GetNextUrl();

        void ProcessResult(GemiUrl url, GemiResponse resp, Exception ex);
    }
}
