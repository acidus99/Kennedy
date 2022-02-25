using System;
using System.Collections.Generic;
using System.Linq;

using Kennedy.CrawlData.Db;
using Kennedy.Crawler.PageRank;

namespace Kennedy.Crawler.Support
{
    public class PageRanker<T>
    {

        int CurrID;
        Dictionary<T, int> NameToIndexMapper;
        Dictionary<int, T> IndexToNameMapper;
        PopRanker PopRanker;

        public PageRanker()
        {
            CurrID = 0;
            NameToIndexMapper = new Dictionary<T, int>();
            IndexToNameMapper = new Dictionary<int, T>();
            PopRanker = new PopRanker();
        }

        public void AddLink(T from, T to)
        {
            int fromID = GetID(from);
            int toID = GetID(to);
            //PageRank.AddLink(fromID, toID);
            PopRanker.AddLink(fromID, toID);
        }

        private int GetID(T page)
        {
            if(!NameToIndexMapper.ContainsKey(page))
            {
                NameToIndexMapper[page] = CurrID;
                IndexToNameMapper[CurrID] = page;
                CurrID++;
            }
            return NameToIndexMapper[page];
        }

        private T GetName(int id)
            => IndexToNameMapper[id];

        public void DoIt()
        {

            var db = new DocIndexDbContext(Crawler.DataDirectory);

            var reachableEntries = db.DocEntries.Where(x => (x.ErrorCount == 0)).ToList();

            var totalPages = reachableEntries.Count;

            Dictionary<long, int> OutboundCount = new Dictionary<long, int>();
            var pl = (from links in db.LinkEntries
                      group links by links.DBSourceDocID into grp
                      select new { key = grp.Key, cnt = grp.Count() });
            foreach( var p in pl)
            {
                OutboundCount[p.key] = p.cnt;
            }





        }


        


        public List<Tuple<T, double>> Rank()
        {
            int pageCount = PopRanker.PageCount;

            List<Tuple<T, double>> ret = new List<Tuple<T, double>>();

            var ranks = PopRanker.RankPages();
            for (int i=0; i < pageCount; i++)
            {
                ret.Add(new Tuple<T, double>(GetName(i), ranks[i]));
            }
            ret.Sort((x, y) => x.Item2.CompareTo(y.Item2));

            return ret;
        }

    }
}
