using System;

using System.Linq;
using System.Collections.Generic;
using Kennedy.SearchIndex.Web;
using Kennedy.SearchIndex.Models;
using System.IO;


namespace Kennedy.Crawler.TopicIndexes
{
    internal class MentionsDumper
    {
        WebDatabaseContext Db;
        TermTracker Tracker;
        string OutDir;

        public MentionsDumper(TermTracker tracker)
        {
            Tracker = tracker;
            Db = new WebDatabaseContext(CrawlerOptions.DataStore);
        }


        public void GenerateFiles(string outputDir, int min)
        {
            OutDir = outputDir;
            Directory.CreateDirectory(outputDir + "nick/");

            var terms = Tracker.GetSortedTerms(min);

            CreateTermPages(terms);
            CreateAlphabIndexPage(terms);
        }

        private string termPath(string term)
            => $"nick/{term}.gmi";


        private string getVariantString(List<string> variants)
            => $"~{variants[0]}";

        private void CreateTermPages(List<Tuple<string, int>> terms)
        {
            foreach (var term in terms)
            {
                var outfile = termPath(term.Item1);
                var variout = getVariantString(Tracker.GetVariations(term.Item1));

                StreamWriter fout = new StreamWriter(OutDir + outfile);
                fout.WriteLine($"# ~{term.Item1}  - Kennedy Mentions Index");
                fout.WriteLine($"Mentions of ~{term.Item1} appear on {term.Item2} pages in gemini space.");
                fout.WriteLine("");
                var urls = Tracker.GetOccurences(term.Item1);
                urls.Sort();
                foreach (var url in urls)
                {
                    var title = (Db.Documents
                        .Where(x => x.UrlID == url.ID)
                        .Select(x => x.Title)
                        .FirstOrDefault());
                    title = title.Length > 0 ? title : $"{url.Hostname}{url.Path}";
                    fout.WriteLine($"=> {url.NormalizedUrl} {title}");
                }
                fout.Close();
            }
        }

        private void CreateAlphabIndexPage(List<Tuple<string, int>> terms)
        {
            StreamWriter index = new StreamWriter(OutDir + "index.gmi");

            index.WriteLine(@"# 👩🏾‍🚀 @Mentions Index

This is an index of mentions (~beingname or @beingname) across gemini space. It goals are:
* Surface content created by a being regardless of where it is in gemini space.
* Promote connections by helping beings find others discussing or responding to their work.

=> about.gmi More about this index
=> icky.gmi This makes me feel icky
=> missing.gmi Something missing? Learn more

## 🔤 Mentions of beings, ordered alphabetically
");

            foreach (var term in terms.OrderBy(x => x.Item1))
            {
                var outfile = termPath(term.Item1);
                var variout = getVariantString(Tracker.GetVariations(term.Item1));

                index.WriteLine($"=> { outfile} {variout} ({term.Item2})");
            }
            index.Close();
        }
    }
}
