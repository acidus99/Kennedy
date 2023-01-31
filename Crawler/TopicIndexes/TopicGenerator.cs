using System;

namespace Kennedy.Crawler.TopicIndexes
{
    /// <summary>
    /// Builds the gemtext files for the Hashtags and Mention indexes
    /// </summary>
    public static class TopicGenerator
    {
        public static void BuildFiles(string outputDirectory)
        {
            TermScanner termScanner = new TermScanner();
            termScanner.ScanDocs();
            HashtagDumper dumper = new HashtagDumper(termScanner.Hashtags);
            dumper.GenerateFiles($"{outputDirectory}hashtags/", 3);

            MentionsDumper mentions = new MentionsDumper(termScanner.Mentions);
            mentions.GenerateFiles($"{outputDirectory}/mentions/", 3);
        }
    }
}
