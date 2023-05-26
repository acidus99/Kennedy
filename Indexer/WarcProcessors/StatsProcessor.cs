using System;

using System.Collections.Generic;

using Gemini.Net;

namespace Kennedy.Indexer.WarcProcessors
{
	public class StatsProcessor : AbstractGeminiWarcProcessor
	{
        Dictionary<string, long> AuthoritySizes;

        Dictionary<string, long> AuthorityCounts;

        Dictionary<string, long> ContentTypeSizes;

        string OutputDir;

        public StatsProcessor(string outDir)
		{
            AuthoritySizes = new Dictionary<string, long>(5000);
            AuthorityCounts = new Dictionary<string, long>(5000);
            ContentTypeSizes = new Dictionary<string, long>(5000);

            OutputDir = outDir;
		}

        public override void FinalizeProcessing()
        {
            OutputStats(OutputDir + "domain-requests.tsv", AuthorityCounts);
            OutputStats(OutputDir + "domain-sizes.tsv", AuthoritySizes);
            OutputStats(OutputDir + "content-sizes.tsv", ContentTypeSizes);
        }

        protected override void ProcessGeminiResponse(GeminiResponse geminiResponse)
        {
            if(geminiResponse.IsSuccess && geminiResponse.MimeType != null)
            {

                var key = geminiResponse.RequestUrl.Authority;

                if(!AuthoritySizes.ContainsKey(key))
                {
                    AuthoritySizes[key] = 0;
                    AuthorityCounts[key] = 0;
                }

                if (!ContentTypeSizes.ContainsKey(geminiResponse.MimeType))
                {
                    ContentTypeSizes[geminiResponse.MimeType] = 0;
                }

                AuthoritySizes[key] += geminiResponse.BodySize;
                AuthorityCounts[key]++;

                ContentTypeSizes[geminiResponse.MimeType] += geminiResponse.BodySize;
            }
        }

        private void OutputStats(string filename, Dictionary<string, long> data)
        {
            StreamWriter fout = new StreamWriter(filename);
            foreach (var item in data)
            {
                fout.WriteLine($"{item.Value}\t{item.Key}");
            }
            fout.Close();

        }
    }
}

