using System;
using System.Text;
using System.Text.RegularExpressions;

using System.Linq;
using System.Collections.Generic;
using Gemini.Net.Crawler.DocumentIndex.Db;
using Gemini.Net.Crawler.DocumentStore;
using Gemini.Net.Crawler.Utils;
using Gemini.Net.Crawler.GemText;
using Gemini.Net;

namespace Gemini.Net.Crawler.Support
{
    public class TermScanner
    {
        DocStore docStore;
        public TermTracker Mentions;
        public TermTracker Hashtags;

        public TermScanner()
        {
            docStore = new DocStore(Crawler.DataDirectory + "page-store/");
            Mentions = new TermTracker();
            Hashtags = new TermTracker();
        }


        public void ScanDocs()
        {

            DocIndexDbContext db = new DocIndexDbContext(Crawler.DataDirectory);
            

            var entries = db.DocEntries
                            .Where(x => (x.BodySaved && x.MimeType.StartsWith("text/gemini"))).ToList()
                            .Select(x => new
                            {
                                DocID = DocumentIndex.DocIndex.toULong(x.DBDocID),
                                Url = new GemiUrl(x.Url)
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

        private void ScanDocument(ulong docID, GemiUrl url)
        {
            var body = docStore.GetDocument(docID);
            var bodyText = Encoding.UTF8.GetString(body);
            ScanDocument(url, bodyText);
        }

        public void ScanDocument(GemiUrl url, string bodyText)
        {

            var tags = HashtagFinder.GetHashtags(bodyText);
            var mentions = MentionsFinder.GetMentions(bodyText);

            Hashtags.AddRange(url, tags);
            Mentions.AddRange(url, mentions);
        }
    }
}
