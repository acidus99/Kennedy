using System;
using Gemini.Net;
using Kennedy.Data;

namespace Kennedy.SearchIndex
{
	public interface IDocumentStorage
	{
        public void Finalize();

        public void StoreDocument(ParsedResponse parsedResponse);

		public void StoreDomain(DomainInfo domainInfo);

		
	}
}

