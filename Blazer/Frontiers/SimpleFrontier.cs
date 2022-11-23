using System;
using System.Collections.Generic;
using System.Linq;

using Gemini.Net;

namespace Kennedy.Blazer.Frontiers
{
    public class SimpleFrontier : IUrlFrontier
    {
        Queue<GeminiUrl> urls;

        public SimpleFrontier()
        {
            urls = new Queue<GeminiUrl>();
        }

        public int Count
            => urls.Count;

        public void AddUrl(GeminiUrl url)
        {
            urls.Enqueue(url);
        }

        public GeminiUrl GetUrl()
        {
            if(urls.Count > 0)
            {
                return urls.Dequeue();
            }
            return null;
        }
    }
}

