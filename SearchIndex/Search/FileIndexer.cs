using System;
using System.Linq;
using System.Text;
using Gemini.Net;
using Kennedy.Data;
using Kennedy.SearchIndex.Web;
using Microsoft.EntityFrameworkCore;

namespace Kennedy.SearchIndex.Search;

public class FileIndexer
{
    string storageDirectory;
    ISearchDatabase searchDatabase;
    PathTokenizer pathTokenizer;

    public FileIndexer(string storageDirectory, ISearchDatabase searchDatabase)
    {
        this.storageDirectory = storageDirectory;
        this.searchDatabase = searchDatabase;
        pathTokenizer = new PathTokenizer();
    }

    public void IndexFiles()
    {
        using (var db = GetContext())
        {
            var indexableFiles = db.IndexableFiles.FromSql(@$"select UrlID, url, LinkText From Documents 
left join Links
on Links.TargetUrlID = Documents.UrlID
where IsBodyIndexed = false and StatusCode = 20 and ContentType != {ContentType.Image}
order by UrlID");

            int total = indexableFiles.Count();
            int counter = 0;

            GeminiUrl? currUrl = null;
            StringBuilder sb = new StringBuilder(1000); //reasonable size for URL + link text
            foreach (var file in indexableFiles)
            {
                counter++;
                if (counter % 100 == 0)
                {
                    Console.WriteLine($"indexing files\t{counter} of {total}");
                }

                //is it a new url?
                if (currUrl == null || file.UrlID != currUrl.ID)
                {
                    if (currUrl != null)
                    {
                        //the last thing to add is the indexable text from the url path
                        sb.Append(' ');
                        sb.Append(GetPathIndexText(currUrl));
                        searchDatabase.UpdateIndexForUrl(currUrl.ID, sb.ToString());
                    }
                    sb.Clear();
                    currUrl = new GeminiUrl(file.Url);
                }

                if (file.LinkText?.Length > 0)
                {
                    if (!sb.ToString().Contains(file.LinkText))
                    {
                        sb.Append(' ');
                        sb.Append(file.LinkText);
                    }
                }
            }
            //handle the remaining buffer
            if (currUrl != null)
            {
                //the last thing to add is the indexable text from the url path
                sb.Append(' ');
                sb.Append(GetPathIndexText(currUrl));
                //full the buffer
                searchDatabase.UpdateIndexForUrl(currUrl.ID, sb.ToString());
            }
        }
    }

    private string GetPathIndexText(GeminiUrl url)
    {
        string[] tokens = pathTokenizer.GetTokens(url);
        return (tokens != null) ? string.Join(' ', tokens) + " " : "";
    }

    private WebDatabaseContext GetContext()
        => new WebDatabaseContext(storageDirectory);
}