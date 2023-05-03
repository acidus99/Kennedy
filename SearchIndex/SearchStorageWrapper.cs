using System;

using Gemini.Net;
using Kennedy.Data;

using Kennedy.SearchIndex.Search;
using Kennedy.SearchIndex.Storage;
using Kennedy.SearchIndex.Web;

namespace Kennedy.SearchIndex
{
	/// <summary>
	/// Wraps the Search Database and Web Database
	/// so you have a single interface to add/update documents and search indexes
	/// </summary>
	public class SearchStorageWrapper
	{
		ISearchDatabase SearchDB;

		IWebDatabase WebDB;

		bool anyContentChanged;

        public SearchStorageWrapper(string storageDirectory)
		{
			WebDB = new WebDatabase(storageDirectory);
			//searchDB has to be after WebDB, because the WebDB DB initialization creates the tables for the entities
			SearchDB = new SearchDatabase(storageDirectory);
			anyContentChanged = false;
        }

		public bool StoreResponse(ParsedResponse response)
		{
			bool contentUpdated = WebDB.StoreResponse(response);
			if(contentUpdated)
			{
				anyContentChanged = true;
                SearchDB.UpdateIndex(response);
            }

			return contentUpdated;
		}

		public void StoreDomain(ServerInfo domain)
			=> WebDB.StoreServer(domain);

		public void FinalizeDatabases()
		{
			if (anyContentChanged)
			{
				SearchDB.IndexImages();
				PopularityCalculator popularityCalculator = new PopularityCalculator(WebDB.GetContext());
				popularityCalculator.Rank();
			}
		}
	}
}

