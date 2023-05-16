using System;
namespace Kennedy.Gemipedia
{
    public class ArticleSummary
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string ThumbnailUrl { get; set; }

        /// <summary>
        /// Snippet of text where search term was found. Usually less helpful than description
        /// </summary>
        public string Excerpt { get; set; }

        public bool HasSummary
            =>!string.IsNullOrEmpty(SummaryText);

        public string SummaryText
            => !String.IsNullOrEmpty(Description) ? Description : Excerpt;
    }
}
