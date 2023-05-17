using System;
namespace Kennedy.Gemipedia
{
    public class ArticleSummary
    {
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required string ThumbnailUrl { get; set; }

        /// <summary>
        /// Snippet of text where search term was found. Usually less helpful than description
        /// </summary>
        public required string Excerpt { get; set; }

        public bool HasSummary
            =>!string.IsNullOrEmpty(SummaryText);

        public string SummaryText
            => !String.IsNullOrEmpty(Description) ? Description : Excerpt;
    }
}
