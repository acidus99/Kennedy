using System;

using Kennedy.CrawlData;

namespace Kennedy.SearchConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            string query = "";
            while (query != "exit")
            {
                Console.WriteLine("Entry Search term");
                query = Console.ReadLine();

                FullTextSearchEngine engine = new FullTextSearchEngine("/var/gemini/crawl-data/");

                var results = engine.DoSearch(query,0,15);

                int counter = 0;

                foreach (var result in results)
                {
                    counter++;

                    Console.WriteLine($"#\t{counter}");
                    Console.WriteLine($"Title\t{result.Title}");
                    Console.WriteLine($"Size\t{result.BodySize}");
                    Console.WriteLine($"Url\t{result.Url}");
                    Console.WriteLine($"Snippet===\n{result.Snippet}\n===");
                    Console.WriteLine();
                    if (counter >= 10)
                    {
                        break;
                    }
                }
            }

        }
    }
}
