using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using Kennedy.Crawler.GemText;
namespace Kennedy.Crawler.Support
{
    public class FeedFinder
    {
        const string storage = "/Users/billy/tmp/crawl-data/";
        DocumentStore docStore = new DocumentStore(storage + "page-store/");
        Regex regex = new Regex(@"^\d{4}-\d{2}-\d{2}\s+");

        public FeedFinder()
        {


        }

        public void Doit()
        {
            var urls = FindGeminiFeeds();
            System.IO.File.WriteAllLines("/Users/billy/tmp/gemfeeds.txt", urls);
            urls = FindXmlFeeds();
            System.IO.File.WriteAllLines("/Users/billy/tmp/atom-feeds.txt", urls);
        }

        public List<string> FindXmlFeeds()
        {
            DocIndexDbContext db = new DocIndexDbContext(storage);

            return db.DocEntries
                .Where(x => (x.ErrorCount == 0 && x.MimeType.StartsWith("application/xml")))
                .Select(x=>x.Url).ToList();
        }

        public List<string> FindGeminiFeeds()
        {
            List<string> ret = new List<string>();

            DocIndexDbContext db = new DocIndexDbContext(storage);

            var entries = db.DocEntries
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

        public bool IsValidGemPub(StoredDocEntry entry)
        {
            entry.SetDocID();
            //try and grab from doc store
            string bodyText = System.Text.Encoding.UTF8.GetString(docStore.GetDocument(entry.DocID));
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

