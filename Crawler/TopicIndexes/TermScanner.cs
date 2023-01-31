using System;
using System.Text;
using System.Linq;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;
using Kennedy.Parsers.GemText;

namespace Kennedy.Crawler.TopicIndexes
{
    internal class TermScanner
    {
        DocumentStore docStore;
        public TermTracker Mentions;
        public TermTracker Hashtags;

        public TermScanner()
        {
            docStore = new DocumentStore(CrawlerOptions.DataStore + "page-store/");
            Mentions = new TermTracker();
            Hashtags = new TermTracker();
        }

        public void ScanDocs()
        {

            DocIndexDbContext db = new DocIndexDbContext(CrawlerOptions.DataStore);
            

            var entries = db.DocEntries
                            .Where(x => (x.BodySaved && x.MimeType.StartsWith("text/gemini"))).ToList()
                            .Where(x=>(x.Domain != "kennedy.gemi.dev"))
                            .Select(x => new
                            {
                                DocID = DocumentIndex.toULong(x.DBDocID),
                                Url = new GeminiUrl(x.Url)
                            }).ToList();

            int total = entries.Count;
            int counter = 0;
            foreach(var entry in entries)
            {
                counter++;
                if(counter % 100 == 0)
                {
                    Console.WriteLine($"{counter}\tof\t{total}\t|\tHashtags: {Hashtags.TermCount} on {Hashtags.UrlCount}\tMentions: {Mentions.TermCount} on {Mentions.UrlCount}");
                }
                ScanDocument(entry.DocID, entry.Url);
            }
        }

        private void ScanDocument(ulong docID, GeminiUrl url)
        {
            var body = docStore.GetDocument(docID);
            var bodyText = Encoding.UTF8.GetString(body);
            ScanDocument(url, bodyText);
        }

        public void ScanDocument(GeminiUrl url, string bodyText)
        {

            var tags = HashtagFinder.GetHashtags(bodyText);
            var mentions = MentionsFinder.GetMentions(bodyText);

            Hashtags.AddRange(url, tags);
            Mentions.AddRange(url, mentions);
        }
    }
}
