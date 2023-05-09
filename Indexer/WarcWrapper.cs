using System;

using Warc;

namespace Kennedy.Indexer
{
	public class WarcWrapper
	{
		object locker = new object();

		WarcParser Parser;

		public int Processed = 0;

		public WarcWrapper(WarcParser warcParser)
		{
			Parser = warcParser;
		}

		DateTime prev = DateTime.Now;

		public WarcRecord? GetNext()
		{

			WarcRecord? ret = null;

			ret = Parser.GetNext();
            if (ret != null)
			{
				Processed++;
				if (Processed % 100 == 0)
				{
					Console.WriteLine($"{Parser.Filename}\t{Processed}\t{Math.Truncate(DateTime.Now.Subtract(prev).TotalMilliseconds)} ms");
					prev = DateTime.Now;
				}
            }
			return ret;
		}
	}
}

