using System;

using Gemini.Net;
using Kennedy.Data;

using Kennedy.SearchIndex.Search;
using Kennedy.SearchIndex.Storage;
using Kennedy.SearchIndex.Web;

namespace Kennedy.SearchIndex
{
	/// <summary>
	/// Wraps the Search Database, Web Database, and Document Storage system
	/// so you have a single interface to add new responses or remove responses
	/// </summary>
	public class SearchStorageWrapper
	{
		public ISearchDatabase SearchDB { get; private set; }

        public IWebDatabase WebDB { get; private set; }

        public IDocumentStore DocumentStore { get; private set; }

        public SearchStorageWrapper(string storageDirectory)
		{
			SearchDB = new SearchDatabase(storageDirectory);
			WebDB = new WebDatabase(storageDirectory);
			DocumentStore = new DocumentStore(storageDirectory + "page-store/");
        }

		public void AddResponse(ParsedResponse response)
		{
			bool IsSaved = DocumentStore.StoreDocument(response);
			WebDB.StoreResponse(response, IsSaved);
			SearchDB.AddToIndex(response);
		}

		public void RemoveResponse(GeminiUrl url)
		{
			DocumentStore.RemoveDocument(url.ID);
			WebDB.RemoveResponse(url);
			SearchDB.RemoveFromIndex(url.ID);
		}

		public void FinalizeDatabases()
		{
			SearchDB.IndexImages();
			PopularityCalculator popularityCalculator = new PopularityCalculator(WebDB.Context);
			popularityCalculator.Rank();
		}
	}
}

