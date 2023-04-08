using System;
using Kennedy.Data;
using System.Security.Cryptography;

using Gemini.Net;

namespace Kennedy.SearchIndex.Storage
{
	public interface IDocumentStore
	{
        /// <summary>
        /// Stores the body of the response. returns if we successfully stored this response
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        bool StoreDocument(ParsedResponse resp);

        public byte[] GetDocument(GeminiUrl url);

        /// <summary>
        /// Gets the body of a URL that was stored in the system. Or returns null;
        /// </summary>
        /// <param name="urlID"></param>
        /// <returns></returns>
        public byte[] GetDocument(long urlID);
    }
}

