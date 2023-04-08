using System;
using Kennedy.Data;
using System.Security.Cryptography;

using Gemini.Net;

namespace Kennedy.SearchIndex.Storage
{
	public interface IDocumentStore
	{
        /// <summary>
        /// Removes the body of a URL from the system, for a given urlID
        /// </summary>
        /// <param name="urlID"></param>
        /// <returns></returns>
        bool RemoveDocument(long urlID);

        /// <summary>
        /// Stores the body of the response. returns if we successfully stored this response
        /// </summary>
        /// <param name="resp"></param>
        /// <returns></returns>
        bool StoreDocument(ParsedResponse resp);

        /// <summary>
        /// Gets the body of a URL that was stored in the system. Or returns null;
        /// </summary>
        /// <param name="urlID"></param>
        /// <returns></returns>
        public byte[] GetDocument(long urlID);
    }
}

