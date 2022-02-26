using System;

using Kennedy.Crawler.Support.Indexes;

namespace Kennedy.Crawler.Support
{
    //Builds the Mentions and Hashtag indexes
    public static class IndexLoader
    {
        public static void BuildIndexes()
        {

            TermScanner termScanner = new TermScanner();
            termScanner.ScanDocs();
            HashtagDumper dumper = new HashtagDumper(termScanner.Hashtags);
            dumper.GenerateFiles("/var/gemini/capsule/public_root/hashtags/", 3);

            MentionsDumper mentions = new MentionsDumper(termScanner.Mentions);
            mentions.GenerateFiles("/var/gemini/capsule/public_root/mentions/", 3);

        }
    }
}
