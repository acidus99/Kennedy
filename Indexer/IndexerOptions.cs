using System;
namespace Kennedy.Indexer
{
	public class IndexerOptions
	{
        /// <summary>
        /// WARC file to index
        /// </summary>
        public List<string> InputWarcs { get; } = new List<string>();

        /// <summary>
        /// Location of the index
        /// </summary>
        public string OutputLocation { get; set; } = "";

        /// <summary>
        /// Should we index the input for the archive?
        /// </summary>
        public bool ShouldIndexArchive { get; set; } = false;

        /// <summary>
        /// Should we index the input for search?
        /// </summary>
        public bool ShouldIndexCrawl { get; set; } = false;

        public bool ShowHelp { get; set; } = false;
    }
}

