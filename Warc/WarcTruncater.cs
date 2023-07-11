using System;
using Warc;

namespace Kennedy.Warc
{
    /// <summary>
    /// One off class to rewrite WARCs and truncate them
    /// </summary>
	public static class WarcTruncater
    {
		public static void Fix(string inputWarc, string outputWarc)
		{
            int truncateSize = 5 * 1024 * 1024;

            using (WarcWriter writer = new WarcWriter(outputWarc))
            {
                WarcParser parser = new WarcParser(inputWarc);

                WarcRecord? record = null;
                int Processed = 0;

                DateTime prev = DateTime.Now;

                do
                {
                    record = parser.GetNext();
                    if (record != null)
                    {
                        Processed++;
                        if (Processed % 100 == 0)
                        {
                            Console.WriteLine($"{parser.Filename}\t{Processed}\t{Math.Truncate(DateTime.Now.Subtract(prev).TotalMilliseconds)} ms");
                            prev = DateTime.Now;
                        }

                        if (record.ContentLength > truncateSize)
                        {
                            record.ContentBlock = record.ContentBlock!.Take(truncateSize).ToArray();
                            record.Truncated = "length";
                        }

                        writer.Write(record);
                    }
                } while (record != null);
            }
        }

	}
}

