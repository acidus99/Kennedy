using System;
using System.Text;
using Gemi.Net;
using HashDepot;

namespace GemiCrawler.Utils
{
    public static class IDGenerator
    {

        /// <summary>
        /// generates the DocID from a URL
        /// This happens by normalizing the URL and hashing it
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public static ulong GetDocID(GemiUrl url)
            => XXHash.Hash64(Encoding.UTF8.GetBytes(url.NormalizedUrl));

        public static uint GetBodyHash(GemiResponse resp)
            => (resp.HasBody) ? XXHash.Hash32(resp.BodyBytes) : 0;

    }
}
