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
        /// Stores/updates a response in our web database.
        /// </summary>
        /// <param name="parsedResponse"></param>
        /// <returns>true if the response's content changed</returns>
        bool StoreResponse(ParsedResponse parsedResponse);
    }
}

