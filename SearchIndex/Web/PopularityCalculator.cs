using System;
using System.Collections.Generic;
using System.Linq;

using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex;

namespace Kennedy.SearchIndex.Web
{
    public class PopularityCalculator
    {
        WebDatabaseContext db;

        public PopularityCalculator(WebDatabaseContext context)
        {
            db = context;
        }

        Dictionary<long, int> OutboundCount = new Dictionary<long, int>();

        Dictionary<long, List<long>> LinksToPage = new Dictionary<long, List<long>>();

        public void Rank()
        {

            var reachableEntries = db.Documents.Where(x => (x.ErrorCount == 0)).ToList();

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

                if(LinksToPage.ContainsKey(entry.UrlID))
                {
                    foreach (var sourceID in LinksToPage[entry.UrlID])
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
            var outLinks = (from links in db.Links
                            where links.IsExternal
                      group links by links.SourceUrlID into grp
                      select new { DBDocID = grp.Key, Count = grp.Count() });
            foreach (var page in outLinks)
            {
                OutboundCount[page.DBDocID] = page.Count;
            }
        }

        private void BuildLinkToPageCache()
        {
            foreach (var link in db.Links.Where(x => (x.IsExternal)))
            {
                if(!LinksToPage.ContainsKey(link.TargetUrlID))
                {
                    LinksToPage[link.TargetUrlID] = new List<long>();
                }
                LinksToPage[link.TargetUrlID].Add(link.SourceUrlID);
            }
        }
    }
}
