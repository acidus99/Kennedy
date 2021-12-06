using System;
using Gemi.Net;


namespace GemiCrawler
{
    public interface IUrlFrontier
    {

        void AddUrl(GemiUrl url);

        int GetCount();

        GemiUrl GetUrl(int crawlerID = 0);

        void OutputStatus(string outputFile);
    }
}
