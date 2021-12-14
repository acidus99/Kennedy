using System;
using Gemi.Net;
using System.Collections.Generic;
namespace GemiCrawler.MetaStore
{
    /// <summary>
    /// Generic interface for storing meta data about requests/responses
    /// </summary>
    public interface IMetaStore
    {
        void StoreMetaData(GemiUrl url, GemiResponse resp, List<GemiUrl> foundLinks);

        void Close();
    }
}
