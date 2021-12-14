using System;
using Gemi.Net;

namespace GemiCrawler.UrlFrontiers
{
    public interface IUrlFrontier
    {

        void AddUrl(GemiUrl url);

        int GetCount();

        GemiUrl GetUrl(int crawlerID = 0);
    }
}
