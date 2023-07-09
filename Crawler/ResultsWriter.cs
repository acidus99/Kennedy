using System;

using Gemini.Net;
using Kennedy.Warc;

using Warc;

using System.Collections.Concurrent;
using System.Collections.Specialized;
using Kennedy.Data;

namespace Kennedy.Crawler
{
	/// <summary>
	/// Holds responses and flushes them to a WARC file
	/// </summary>
	public class ResultsWriter
	{
		ConcurrentQueue<Tuple<GeminiResponse, TlsConnectionInfo?>> responses;
		GeminiWarcCreator warcCreator;

		public int Saved { get; private set; }

		public ResultsWriter(string warcDirectory)
		{
			Saved = 0;
			responses = new ConcurrentQueue<Tuple<GeminiResponse, TlsConnectionInfo?>>();
			warcCreator = new GeminiWarcCreator(warcDirectory + DateTime.Now.ToString("yyyy-MM-dd") + ".warc");
			warcCreator.WriteWarcInfo(new WarcFields
			{
				{"software", "Kennedy Crawler"},
				{"hostname", "kennedy.gemi.dev"},
				{"timestamp", DateTime.Now },
				{"operator", "Acidus"}
			});
        }

		public void AddToQueue(GeminiResponse response, TlsConnectionInfo? connectionInfo)
		{
			responses.Enqueue(new Tuple<GeminiResponse, TlsConnectionInfo?>(response, connectionInfo));
		}

		public void Flush()
		{
			Tuple<GeminiResponse, TlsConnectionInfo?>? response;
			while(responses.TryDequeue(out response))
			{
				WriteResponseToWarc(response);
			}
		}

		public void Close()
		{
			Flush();
			warcCreator.Dispose();
		}

		private void WriteResponseToWarc(Tuple<GeminiResponse, TlsConnectionInfo?> response)
		{
			warcCreator.WriteSession(response.Item1, response.Item2);
            Saved++;
        }
	}
}

