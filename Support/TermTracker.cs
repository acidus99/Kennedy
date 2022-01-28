using System;
using System.Text;
using System.Text.RegularExpressions;

using System.Linq;
using System.Collections.Generic;
using GemiCrawler.DocumentIndex.Db;
using GemiCrawler.DocumentStore;
using GemiCrawler.Utils;
using GemiCrawler.GemText;
using Gemini.Net

namespace GemiCrawler.Support
{
    public class TermTracker
    {
        public Dictionary<string, HashSet<GemiUrl>> pages;
        public Dictionary<string, Bag<string>> variations;

        public TermTracker()
        {
            pages = new Dictionary<string, HashSet<GemiUrl>>();
            variations = new Dictionary<string, Bag<string>>();
            UrlCount = 0;
        }

        public int TermCount => pages.Keys.Count;

        public int UrlCount { get; private set; }

        public void AddRange(GemiUrl onUrl, IEnumerable<string> terms)
            => terms.ToList().ForEach(x => Add(onUrl, x));

        public void Add(GemiUrl onUrl, string term)
        {
            var termlow = term.ToLower();
            if(!pages.ContainsKey(termlow))
            {
                variations[termlow] = new Bag<string>();
                pages[termlow] = new HashSet<GemiUrl>();
            }
            pages[termlow].Add(onUrl);
            variations[termlow].Add(term);
            UrlCount++;
        }

        public List<GemiUrl> GetOccurences(string term)
            => (pages.ContainsKey(term)) ? pages[term].ToList() : null;

        public List<string> GetVariations(string term)
            => (variations.ContainsKey(term)) ? variations[term].GetSortedValues().Select(x=>x.Item1).ToList() : null;

        public List<Tuple<string, int>> GetSortedTerms(int atLeast = 1)
            => pages.Keys.Select(x => new Tuple<string, int>(x, pages[x].Count)).ToList()
                .Where(x => (x.Item2 >= atLeast)).OrderByDescending(x => x.Item2).ToList();

    }
}
