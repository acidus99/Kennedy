using System;
using System.Linq;


using Gemini.Net;
using Kennedy.SearchIndex.Models;
using Kennedy.SearchIndex.Indexers;
using Kennedy.SearchIndex.Engines;

using Kennedy.Data;

namespace Kennedy.SearchIndex
{
	/// <summary>
    /// Allows documents to be 
    /// </summary>
	public class DocumentStorageSystem : IDocumentStorage
	{
        //holds meta data about the documents
        DocumentIndex documentIndex;

        //holds the raw contents
        DocumentStore documentStore;

        FullTextSearchEngine ftsEngine;

        ImageIndexer imageIndexer;


        public DocumentStorageSystem(string dataDirectory)
		{
            documentIndex = new DocumentIndex(dataDirectory);
            documentStore = new DocumentStore(dataDirectory + "page-store/");
            ftsEngine = new FullTextSearchEngine(dataDirectory);
            imageIndexer = new ImageIndexer(documentIndex);
        }

        public void Finalize()
        {
            //Do Image Indexing
            imageIndexer.IndexImages();

            //Do population calculations
            PopularityCalculator popularity = new PopularityCalculator(documentIndex);
            popularity.Rank();
        }

        public void StoreDocument(ParsedResponse parsedResponse)
        {
            //store the response in our document store, allowing us to do cool things like serve cached copies
            bool isBodySaved = StoreFullResponse(parsedResponse);

            StoreMetaData(parsedResponse, isBodySaved);

            //Index the text
            IndexResponse(parsedResponse);
        }

        public void StoreDomain(DomainInfo domainInfo)
        {
            using (var db = documentIndex.GetContext())
            {
                db.Domains.Add(
                    new Domain
                    {
                        DomainName = domainInfo.Domain,
                        Port = domainInfo.Port,

                        IsReachable = domainInfo.IsReachable,

                        HasFaviconTxt = domainInfo.HasFaviconTxt,
                        HasRobotsTxt = domainInfo.HasRobotsTxt,
                        HasSecurityTxt = domainInfo.HasSecurityTxt,

                        FaviconTxt = domainInfo.FaviconTxt,
                        RobotsTxt = domainInfo.RobotsTxt,
                        SecurityTxt = domainInfo.SecurityTxt
                    });
                db.SaveChanges();
            }
        }

        private void IndexResponse(ParsedResponse parsedResponse)
        {
            if (parsedResponse is GemTextResponse)
            {
                GemTextResponse gemText = parsedResponse as GemTextResponse;
                if (gemText.IsIndexable)
                {
                    ftsEngine.AddResponseToIndex(parsedResponse.RequestUrl.ID, gemText.Title, gemText.FilteredBody);
                }
            }
        }

        private bool StoreFullResponse(ParsedResponse response)
            => documentStore.StoreDocument(response);

        private void StoreMetaData(ParsedResponse parsedResponse, bool isBodySaved)
        {
            //store in in the doc index (inserting or updating as appropriate)
            documentIndex.StoreMetaData(parsedResponse, isBodySaved);

            //if its an image, we store extra meta data
            if (parsedResponse is ImageResponse)
            {
                documentIndex.StoreImageMetaData(parsedResponse as ImageResponse);
            }

            //store the links
            documentIndex.StoreLinks(parsedResponse); 
        }
    }
}
