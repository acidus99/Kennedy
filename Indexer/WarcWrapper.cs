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


		public WarcRecord? GetNext()
		{

			WarcRecord? ret = null;

			lock (locker)
			{
				ret = Parser.GetNext();
				if (ret != null)
				{
					Processed++;
                    if (Processed % 100 == 0) Console.WriteLine($"Ingesting\t{Processed}");
                }
			}
			return ret;
		}
	}
}

