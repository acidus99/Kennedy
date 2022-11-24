﻿using System;
using System.Collections.Generic;
using System.Linq;

using Kennedy.CrawlData.Db;
using Kennedy.CrawlData;

namespace Kennedy.CrawlData.Indexers
{
    public class PopularityCalculator
    {
        DocIndexDbContext db;

        public PopularityCalculator(DocumentIndex documentIndex)
        {
            db = documentIndex.GetContext();
        }

        Dictionary<long, int> OutboundCount = new Dictionary<long, int>();

        Dictionary<long, List<long>> LinksToPage = new Dictionary<long, List<long>>();

        public void Rank()
        {

            var reachableEntries = db.DocEntries.Where(x => (x.ErrorCount == 0)).ToList();

            var totalPages = reachableEntries.Count;

            Console.WriteLine("Building caches");
            BuildOutlinkCache();
            BuildLinkToPageCache();

            Console.WriteLine("computing popularity");

            foreach(var entry in reachableEntries)
            {
                //every page has a rank of 1
                entry.PopularityRank = 1;
                entry.ExternalInboundLinks = 0;

                if(LinksToPage.ContainsKey(entry.DBDocID))
                {
                    foreach (var sourceID in LinksToPage[entry.DBDocID])
                    {
                        //they get 1 more for each cross domain link
                        //var voteValue = (1 / OutboundCount[sourceID]);
                        var voteValue = 1;
                        entry.ExternalInboundLinks++;
                        entry.PopularityRank += voteValue;
                    }
                }
            }
            Console.WriteLine("computing percentages");
            foreach (var entry in reachableEntries)
            {
                //clip to 100
                entry.PopularityRank = (entry.PopularityRank > 100) ? 100 : entry.PopularityRank;
                //log distribution over the score
                entry.PopularityRank = Math.Log(entry.PopularityRank, 100);
            }
            db.SaveChanges();
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