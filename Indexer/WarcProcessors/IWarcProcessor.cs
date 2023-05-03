using System;

using Toimik.WarcProtocol;

namespace Kennedy.Indexer.WarcProcessors
{
	public interface IWarcProcessor
	{
		public void ProcessRecord(Record record);

		public void FinalizeProcessing();
	}
}

