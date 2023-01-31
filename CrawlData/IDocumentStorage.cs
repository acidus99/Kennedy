using System;
using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.CrawlData
{
	public interface IDocumentStorage
	{
		public void StoreDocument(ParsedResponse parsedResponse);

		public void StoreDomain(DomainInfo domainInfo);

		public void Finalize();
	}
}

