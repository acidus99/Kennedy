using System;

using Gemini.Net;
using Warc;

namespace Kennedy.Warc
{

    public class MimeStat
    {
        public required string MimeType { get; set; }
        public long TotalSize { get; set; } = 0;
        public long Count { get; set; } = 0;
        public long Savings { get; set; } = 0;
    }

    /// <summary>
    /// One off class to rewrite WARCs and truncate them
    /// </summary>
	public class WarcStats
    {
        Dictionary<string, MimeStat> stats = new Dictionary<string, MimeStat>();

        public void Scan(string inputWarc)
		{
            DateTime prev = DateTime.Now;
            int Processed = 0;

            using (WarcReader reader = new WarcReader(inputWarc))
            {
                foreach(var record in reader)
                {
                    Processed++;
                    if (record is ResponseRecord x)
                    {
                        RecordStats(x);
                    }

                }
            }
            int seconds = (int) DateTime.Now.Subtract(prev).TotalSeconds;
            if(seconds == 0)
            {
                seconds = 1;
            }

            Console.WriteLine($"Records: {Processed}\tTime: {seconds}s\tRate: {Processed / seconds} / s");
        }

        public void WriteResults(string outFile)
        {
            StreamWriter fout = new StreamWriter(outFile, false);
            fout.WriteLine("mimetype,count,total_size,savings_if_100k");
            foreach(var s in stats.Values.OrderBy(x=>x.Count))
            {
                fout.WriteLine($"{s.MimeType},{s.Count},{s.TotalSize},{s.Savings}");
            }

            fout.Close();
        }

        private void RecordStats(ResponseRecord record)
        {
            if(record.IdentifiedPayloadType == null)
            {
                return;
            }

            MimeStat item;

            if (!stats.ContainsKey(record.IdentifiedPayloadType))
            {
                item = new MimeStat
                {
                    Count = 1,
                    MimeType = record.IdentifiedPayloadType,
                    TotalSize = record.ContentLength
                };

                stats[record.IdentifiedPayloadType] = item;
            }
            else
            {
                item = stats[record.IdentifiedPayloadType];
                item.Count++;
                item.TotalSize += record.ContentLength;
            }

            if(record.ContentLength > 100 * 1024)
            {
                item.Savings += (record.ContentLength - (100 * 1014));
            }
        }
	}


}

