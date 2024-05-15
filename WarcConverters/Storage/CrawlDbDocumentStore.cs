using System.Security.Cryptography;

namespace Kennedy.WarcConverters.Storage;

/// <summary>
/// Get the contents of a URL based on the hash of the URL.
/// Implemented using an Object Store, backed onto a disk
///
/// This was used by the crawl-db format of crawls
/// </summary>
public class CrawlDbDocumentStore
{
    ObjectStore store;

    public bool Exists { get; set; }

    public CrawlDbDocumentStore(string outputDir)
    {
        store = new ObjectStore(outputDir);
        Exists = Directory.Exists(outputDir);
    }

    public byte[] GetDocument(long urlID)
    {
        var key = GeyKey(urlID);
        return store.GetObject(key);
    }

    private string GeyKey(long urlID)
    {
        //hack, we used to use ulong here. continue that here so we can read old page-store directories
        ulong legacyUrlID = unchecked((ulong)urlID);
        return Convert.ToHexString(MD5.HashData(BitConverter.GetBytes(legacyUrlID))).ToLower();
    }
}