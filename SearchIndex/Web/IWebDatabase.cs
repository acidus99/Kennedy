using System;

using Gemini.Net;

using Kennedy.Data;
using Kennedy.SearchIndex.Models;

namespace Kennedy.SearchIndex.Web
{
	public interface IWebDatabase
	{
        WebDatabaseContext GetContext();

        /// <summary>
        /// Stores data about the response, and its links to other resources
        /// </summary>
        /// <param name="parsedResponse"></param>
        void StoreResponse(ParsedResponse parsedResponse, bool bodyWasSaved);

        void StoreDomain(DomainInfo domainInfo);

        bool RemoveResponse(GeminiUrl url);
    }
}

