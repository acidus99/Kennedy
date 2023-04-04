using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Gemini.Net;
using Kennedy.SearchIndex;
using Kennedy.SearchIndex.Models;
using Kennedy.Data;
using Kennedy.Parsers.GemText;

namespace Kennedy.Crawler.Support
{
    /// <summary>
    /// Detects all gemini feeds and Atom feeds. For gemini feeds, finds all the gemtext, the looks in
    /// the page store to if they have link which match the gemini feed format
    /// </summary>
    public class FeedFinder
    {
        DocumentStore docStore = new DocumentStore(CrawlerOptions.DataStore+ "page-store/");
        Regex regex = new Regex(@"^\d{4}-\d{2}-\d{2}\s+");

        public void Doit()
        {
            var urls = FindGeminiFeeds();
            urls = FindXmlFeeds();
            //TODO: 
            //Write out the URLS
        }

        public List<string> FindXmlFeeds()
        {
            SearchIndexContext db = new SearchIndexContext(CrawlerOptions.DataStore);

            return db.Documents
                .Where(x => (x.ErrorCount == 0 && x.MimeType.StartsWith("application/xml")))
                .Select(x=>x.Url).ToList();
        }

        public List<string> FindGeminiFeeds()
        {
            List<string> ret = new List<string>();

            SearchIndexContext db = new SearchIndexContext(CrawlerOptions.DataStore);

            var entries = db.Documents
                .Where(x => (x.ErrorCount == 0 && x.BodySize > 0 && x.MimeType.StartsWith("text/gemini"))).ToList();

            int total = entries.Count;
            int curr = 0;
            int hits = 0;
            foreach (var entry in entries)
            {
                curr++;
                if (curr % 100 == 0)
                {
                    Console.WriteLine($"{curr}\tof\t{total}\tHits:\t{hits}");
                }
                if(IsValidGemPub(entry))
                {
                    hits++;
                    ret.Add(entry.Url);
                }
            }
            return ret;
        } 

        public bool IsValidGemPub(Document entry)
        {
            //try and grab from doc store
            string bodyText = System.Text.Encoding.UTF8.GetString(docStore.GetDocument(entry.UrlID));
            if (bodyText.Length > 0)
            {
                var gurl = new GeminiUrl(entry.Url);

                foreach (var link in LinkFinder.ExtractBodyLinks(gurl, bodyText))
                {
                    if (IsValidGemPubLink(link))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsValidGemPubLink(FoundLink link)
            => regex.IsMatch(link.LinkText);
            
    }
}

