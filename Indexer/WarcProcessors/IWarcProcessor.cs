﻿using System;

using Warc;

namespace Kennedy.Indexer.WarcProcessors
{
	public interface IWarcProcessor
	{
		public void ProcessRecord(WarcRecord record);

		public void FinalizeProcessing();
	}
}
