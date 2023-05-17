using System;
using Kennedy.Data;
using System.Security.Cryptography;

using Gemini.Net;

namespace Kennedy.AdminConsole.Storage
{
	public interface IDocumentStore
	{
        /// <summary>
        /// Gets the body of a URL that was stored in the system. Or returns null;
        /// </summary>
        /// <param name="urlID"></param>
        /// <returns></returns>
        public byte[] GetDocument(long urlID);
    }
}

