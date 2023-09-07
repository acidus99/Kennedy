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
        const int MaxUninterestingFileSize = 10 * 1024;

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
			var optimizedResponse = OptimizeForStoage(response.Item1);
			warcCreator.WriteSession(optimizedResponse, response.Item2);
            Saved++;
        }

        private GeminiResponse OptimizeForStoage(GeminiResponse response)
        {
            if (!response.HasBody || response.MimeType == null)
            {
                return response;
            }

            if(response.MimeType.StartsWith("text/") || response.MimeType.StartsWith("image/"))
            {
                return response;
            }

            if(response.BodySize > MaxUninterestingFileSize)
            {
                response.BodyBytes = response.BodyBytes!.Take(MaxUninterestingFileSize).ToArray();
                response.IsBodyTruncated = true;
            }

            return response;
        }

    }
}

