using System;
namespace Kennedy.Data
{
	public interface ITextResponse
	{
        public string? DetectedLanguage { get; }

        public bool HasIndexableText { get; }

        /// <summary>
        /// Is this response a feed of items (e.g. RSS, Gemfeed, Atom, etc.)
        /// </summary>
        public bool IsFeed { get; }

        public string? IndexableText { get; }

        public int LineCount { get; }

        public string? Title { get; }

	}
}

