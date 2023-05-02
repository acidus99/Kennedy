using System;

using Gemini.Net;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex;
using Kennedy.Data.Parsers;
using Kennedy.Data;
using Toimik.WarcProtocol;

namespace Kennedy.Indexer
{
	public class RecordIngester
	{

		Dictionary<string, DomainInfo> Domains;

		SearchStorageWrapper searchWrapper;
		ResponseParser responseParser;

		public RecordIngester(string storageDirectory, string configDirectory)
		{
            LanguageDetector.ConfigFileDirectory = configDirectory;
            searchWrapper = new SearchStorageWrapper(storageDirectory);
			responseParser = new ResponseParser();
			Domains = new Dictionary<string, DomainInfo>();
			
		}

        public void Ingest(Record record)
		{
			if(record is ResponseRecord)
			{
				Ingest((ResponseRecord) record);
			}
		}

		public void CompleteImport()
		{
			Console.WriteLine("In Complete Import");

            foreach (DomainInfo domainInfo in Domains.Values)
            {
				searchWrapper.WebDB.StoreDomain(domainInfo);
            }

            searchWrapper.FinalizeDatabases();
		}

        private void Ingest(ResponseRecord responseRecord)
		{
			if (responseRecord.ContentBlock != null)
			{
				var response = ParseResponseRecord(responseRecord);
				searchWrapper.AddResponse(response);
				HandleDomain(response);
			}
		}

		private ParsedResponse ParseResponseRecord(ResponseRecord record)
		{
            GeminiUrl url = new GeminiUrl(record.TargetUri);
			var parsedResponse = responseParser.Parse(url, record.ContentBlock!);
			parsedResponse.RequestSent = record.Date;
            parsedResponse.ResponseReceived = record.Date;
			if(!string.IsNullOrEmpty(record.TruncatedReason))
			{
				parsedResponse.IsBodyTruncated = true;
			}

			return parsedResponse;
        }

		private void HandleDomain(ParsedResponse response)
		{
			string key = response.RequestUrl.Authority;

			if (!Domains.ContainsKey(key))
			{
				bool isReachable = IsReachable(response);

                Domains.Add(key, new DomainInfo
				{
					Domain = response.RequestUrl.Hostname,
					Port = response.RequestUrl.Port,
					IsReachable = isReachable,
					ErrorMessage = (isReachable) ? null : response.Meta
				});
			}
			if (response.IsSuccess)
			{
				if (response.RequestUrl.Path == "/robots.txt")
				{
					Domains[key].RobotsUrlID = response.RequestUrl.ID;
				}
				else if (response.RequestUrl.Path == "/favicon.txt" && IsValidFavicon(response.BodyText))
				{
					Domains[key].FaviconUrlID = response.RequestUrl.ID;
					Domains[key].FaviconTxt = response.BodyText;
				}
				else if (response.RequestUrl.Path == "/.well-known/security.txt" && IsValidSecurity(response.BodyText))
				{
					Domains[key].SecurityUrlID = response.RequestUrl.ID;
				}
			}
        }

		private bool IsValidFavicon(string contents)
			=> (contents != null && !contents.Contains(" ") && !contents.Contains("\n") && contents.Length < 20);

		private bool IsValidSecurity(string contents)
		  => (contents != null && contents.ToLower().Contains("contact:"));

        private bool IsReachable(ParsedResponse response)
            => response.StatusCode != GeminiParser.ConnectionErrorStatusCode;

    }
}

