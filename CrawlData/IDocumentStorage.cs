using System;
using Gemini.Net;
using Kennedy.Data.Models;

namespace Kennedy.CrawlData
{
	public interface IDocumentStorage
	{
		public void StoreDocument(GeminiResponse response, AbstractResponse parsedResponse);

		public void StoreDomain(DomainInfo domainInfo);

		public void Finalize();
	}
}

