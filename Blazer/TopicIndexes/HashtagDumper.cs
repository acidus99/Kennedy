using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Gemini.Net;
using Kennedy.CrawlData;
using Kennedy.CrawlData.Db;

namespace Kennedy.Blazer.TopicIndexes
{
    internal class HashtagDumper
    {
        DocIndexDbContext Db;
        TermTracker Tracker;
        string OutDir;

        public HashtagDumper(TermTracker tracker)
        {
            Tracker = tracker;
            Db = new DocIndexDbContext(CrawlerOptions.DataStore);
        }


        public void GenerateFiles(string outputDir, int min)
        {
            OutDir = outputDir;
            Directory.CreateDirectory(outputDir + "tags/");

            var terms = Tracker.GetSortedTerms(min);

            CreateTermPages(terms);
            CreatePopularIndexPage(terms);
            CreateAlphabIndexPage(terms);            
        }

        private string termPath(string term)
            => $"tags/{term}.gmi";


        private string getVariantString(List<string> variants)
        {
            var variout = $"#{variants[0]}";
            if (variants.Count > 1)
            {
                variout += $" (and #{variants[1]}";
                for (int i = 2; i < variants.Count; i++)
                {
                    variout += $", #{variants[i]}";
                }
                variout += ")";
            }
            return variout;
        }

        private void CreateTermPages(List<Tuple<string, int>> terms)
        {
            foreach (var term in terms)
            {
                var outfile = termPath(term.Item1);
                var variout = getVariantString(Tracker.GetVariations(term.Item1));

                StreamWriter fout = new StreamWriter(OutDir + outfile);
                fout.WriteLine($"# #{term.Item1} - Kennedy #Hashtag Index");
                fout.WriteLine($"The hashtag #{term.Item1} appears on {term.Item2} pages in gemini space.");
                fout.WriteLine("Detected variants: " + variout);
                var urls = Tracker.GetOccurences(term.Item1);
                urls.Sort();
                foreach (var url in urls)
                {
                    var dbID = DocumentIndex.toLong(url.HashID);
                    var title = (Db.DocEntries.Where(x => x.DBDocID == dbID).Select(x => x.Title).FirstOrDefault());
                    title = title.Length > 0 ? title : $"{url.Hostname}{url.Path}";
                    fout.WriteLine($"=> {url.NormalizedUrl} {title}");
                }
                fout.Close();
            }
        }

        private void CreatePopularIndexPage(List<Tuple<string, int>> terms)
        {
            StreamWriter index = new StreamWriter(OutDir + "index.gmi");
            index.WriteLine(@$"# 🏷 #Hashtag Index (Occurrence)

This is an index of {terms.Count} commonly used hashtags across gemini space.

=> about.gmi About this index
=> hashtagsAZ.gmi 🔤 Ordered alphabetically

## 📈 Popular Hashtags (at least 3 uses), ordered by occurrence");

            foreach (var term in terms)
            {
                var outfile = termPath(term.Item1);
                var variout = getVariantString(Tracker.GetVariations(term.Item1));

                index.WriteLine($"=> {outfile} {variout} ({term.Item2})");
            }
            index.Close();
        }
        private void CreateAlphabIndexPage(List<Tuple<string, int>> terms)
        {
            StreamWriter index = new StreamWriter(OutDir + "hashtagsAZ.gmi");
            index.WriteLine(@$"# 🏷 #Hashtag Index (Alphabetical)

This is an index of {terms.Count} commonly used hashtags across gemini space.

=> about.gmi About this index
=> index.gmi 📈 Ordered by Popularity  

## 🔤 Popular Hashtags (at least 3 uses), ordered alphabetically");
            foreach (var term in terms.OrderBy(x => x.Item1))
            {
                var outfile = termPath(term.Item1);
                var variout = getVariantString(Tracker.GetVariations(term.Item1));

                index.WriteLine($"=> {outfile} {variout} ({term.Item2})");
            }
            index.Close();
        }
    }
}
