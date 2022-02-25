using System;
using System.Collections.Generic;
using System.Linq;

using Kennedy.CrawlData.Db;
using Kennedy.Crawler.PageRank;

namespace Kennedy.Crawler.Support
{
    public class PopularityCalculator
    {

        DocIndexDbContext db = new DocIndexDbContext(Crawler.DataDirectory);

        Dictionary<long, int> OutboundCount = new Dictionary<long, int>();

        Dictionary<long, List<long>> LinksToPage = new Dictionary<long, List<long>>();

        public void Rank()
        {

            var reachableEntries = db.DocEntries.Where(x => (x.ErrorCount == 0)).ToList();

            var totalPages = reachableEntries.Count;

            Console.WriteLine("Building caches");
            BuildOutlinkCache();
            BuildLinkToPageCache();

            double totalPopularity = 0;
            Console.WriteLine("computing popularity");

            int counter = 0;

            foreach(var entry in reachableEntries)
            {
                counter++;
                if(counter % 100 ==0 )
                {
                    Console.WriteLine($"{counter} of {totalPages}");
                }

                var seedPopularity = 1;

                entry.PopularityRank = seedPopularity;
                totalPopularity += seedPopularity;

                if(LinksToPage.ContainsKey(entry.DBDocID))
                {
                    foreach (var sourceID in LinksToPage[entry.DBDocID])
                    {
                        //var voteValue = (1 / OutboundCount[sourceID]);
                        var voteValue = 1;

                        entry.PopularityRank += voteValue;
                        totalPopularity += voteValue;
                    }
                }
            }
            //Console.WriteLine("computing percentages");
            //foreach (var entry in reachableEntries)
            //{
            //    entry.PopularityRank = entry.PopularityRank / totalPopularity;
            //}

            db.SaveChanges();


            int xxx = 4;


        }

        private void BuildOutlinkCache()
        {
            var outLinks = (from links in db.LinkEntries
                            where links.IsExternal
                      group links by links.DBSourceDocID into grp
                      select new { DBDocID = grp.Key, Count = grp.Count() });
            foreach (var page in outLinks)
            {
                OutboundCount[page.DBDocID] = page.Count;
            }
        }

        private void BuildLinkToPageCache()
        {
            foreach (var link in db.LinkEntries.Where(x => (x.IsExternal)))
            {
                if(!LinksToPage.ContainsKey(link.DBTargetDocID))
                {
                    LinksToPage[link.DBTargetDocID] = new List<long>();
                }
                LinksToPage[link.DBTargetDocID].Add(link.DBSourceDocID);
            }
        }

    }
}
