using System;

using System.Linq;
using System.Collections.Generic;
using Gemini.Net.Crawler.Utils;
using Gemini.Net;

namespace Gemini.Net.Crawler.Support
{
    public class TermTracker
    {
        public Dictionary<string, HashSet<GeminiUrl>> pages;
        public Dictionary<string, Bag<string>> variations;

        public TermTracker()
        {
            pages = new Dictionary<string, HashSet<GeminiUrl>>();
            variations = new Dictionary<string, Bag<string>>();
            UrlCount = 0;
        }

        public int TermCount => pages.Keys.Count;

        public int UrlCount { get; private set; }

        public void AddRange(GeminiUrl onUrl, IEnumerable<string> terms)
            => terms.ToList().ForEach(x => Add(onUrl, x));

        public void Add(GeminiUrl onUrl, string term)
        {
            var termlow = term.ToLower();
            if(!pages.ContainsKey(termlow))
            {
                variations[termlow] = new Bag<string>();
                pages[termlow] = new HashSet<GeminiUrl>();
            }
            pages[termlow].Add(onUrl);
            variations[termlow].Add(term);
            UrlCount++;
        }

        public List<GeminiUrl> GetOccurences(string term)
            => (pages.ContainsKey(term)) ? pages[term].ToList() : null;

        public List<string> GetVariations(string term)
            => (variations.ContainsKey(term)) ? variations[term].GetSortedValues().Select(x=>x.Item1).ToList() : null;

        public List<Tuple<string, int>> GetSortedTerms(int atLeast = 1)
            => pages.Keys.Select(x => new Tuple<string, int>(x, pages[x].Count)).ToList()
                .Where(x => (x.Item2 >= atLeast)).OrderByDescending(x => x.Item2).ToList();

    }
}
