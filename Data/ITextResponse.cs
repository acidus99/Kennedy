using System;
namespace Kennedy.Data
{
	public interface ITextResponse
	{
        public string? DetectedLanguage { get; }

        public bool HasIndexableText { get; }

        public string? IndexableText { get; }

        public int LineCount { get; }

        public string? Title { get; }

	}
}

