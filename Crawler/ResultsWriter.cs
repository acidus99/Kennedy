using System;

using Gemini.Net;
using Kennedy.Warc;

using Warc;

using System.Collections.Concurrent;
using System.Collections.Specialized;

namespace Kennedy.Crawler
{
	/// <summary>
	/// Holds responses and flushes them to a WARC file
	/// </summary>
	public class ResultsWriter
	{
		ConcurrentQueue<GeminiResponse> responses;
		GeminiWarcCreator warcCreator;

		public int Saved { get; private set; }

		public ResultsWriter(string warcDirectory)
		{
			Saved = 0;
			responses = new ConcurrentQueue<GeminiResponse>();
			warcCreator = new GeminiWarcCreator(warcDirectory + "gemini.crawl.warc");
			warcCreator.WriteWarcInfo(new WarcFields
			{
				{"software", "Kennedy Crawler"},
				{"hostname", "kennedy.gemi.dev"},
				{"timestamp", DateTime.Now },
				{"operator", "Acidus"}
			});
        }

		public void AddToQueue(GeminiResponse response)
		{
			responses.Enqueue(response);
		}

		public void Flush()
		{
			GeminiResponse? response;
			while(responses.TryDequeue(out response))
			{
				WriteResponseToWarc(response);
			}
		}

		private void WriteResponseToWarc(GeminiResponse response)
		{
			warcCreator.WriteSession(response);
            Saved++;
        }
	}
}

