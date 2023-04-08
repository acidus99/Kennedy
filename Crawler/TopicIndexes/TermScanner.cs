using System;
using System.Text;
using System.Linq;

using Gemini.Net;
using Kennedy.SearchIndex;
using Kennedy.SearchIndex.Models;
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

            SearchIndexContext db = new SearchIndexContext(CrawlerOptions.DataStore);
            

            var entries = db.Documents
                            .Where(x => (x.BodySaved && x.MimeType.StartsWith("text/gemini"))).ToList()
                            .Where(x=>(x.Domain != "kennedy.gemi.dev"))
                            .Select(x => new
                            {
                                UrlID = (x.UrlID),
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
                ScanDocument(entry.UrlID, entry.Url);
            }
        }

        //TODO: pretty sure I can just use the ID in the gemini URL class
        private void ScanDocument(long urlID, GeminiUrl url)
        {
            var body = docStore.GetDocument(urlID);
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
