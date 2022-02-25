using System;
using System.Linq;
using System.Collections.Generic;
namespace Kennedy.Crawler.PageRank
{
    public class LinkMatrix
    {
        Dictionary<int, int []> Columns;
        Dictionary<int, HashSet<int>> Adds;

        HashSet<int> UniquePages;

        public LinkMatrix()
        {
            Adds = new Dictionary<int, HashSet<int>>();
            UniquePages = new HashSet<int>();
        }

        public void AddLink(int fromPage, int toPage)
        {
            UniquePages.Add(fromPage);
            UniquePages.Add(toPage);

            if (!Adds.ContainsKey(fromPage))
            {
                Adds[fromPage] = new HashSet<int>();
            }
            Adds[fromPage].Add(toPage);
        }

        public int PageCount
            => UniquePages.ToArray().Length;

        public void Prepare()
        {
            Columns = new Dictionary<int, int[]>();
            foreach(var column in Adds.Keys)
            {
                var list = Adds[column].ToList();
                //sort it ASC, so we can do a binary search on it
                list.Sort();
                Columns[column] = list.ToArray();
            }
            Adds.Clear();
        }

        public int OutLinkCount(int fromPage)
        {
            if (!Columns.ContainsKey(fromPage))
            {
                return 0;
            }

            return Columns[fromPage].Length;
        }

        public int [] GetPageLinks(int forPage)
        {
            if(!Columns.ContainsKey(forPage))
            {
                return null;
            }
            return Columns[forPage];
        }

        public bool HasLink(int fromPage, int toPage)
        {
            //if there are no links that come from the fromPage, it cannot link to the toPage
            if(!Columns.ContainsKey(fromPage))
            {
                return false;
            }
            //binary search to quickly see if we link to a page
            return Array.BinarySearch(Columns[fromPage], toPage) > -1;
        } 

    }
}
