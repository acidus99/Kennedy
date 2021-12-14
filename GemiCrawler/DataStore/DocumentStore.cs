using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gemi.Net;
using GemiCrawler.Utils;

using GemiCrawler.Modules;

namespace GemiCrawler
{
    public class DocumentStore : AbstractModule
    {

        string pageStorageDir;
        ThreadSafeCounter failedCounter;
        

        public DocumentStore(string path)
            :base("Document-Store")
        {
            failedCounter = new ThreadSafeCounter();

            pageStorageDir = path;
            if (Directory.Exists(pageStorageDir))
            {
                DirectoryInfo di = new DirectoryInfo(pageStorageDir);

                foreach (FileInfo file in di.EnumerateFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.EnumerateDirectories())
                {
                    dir.Delete(true);
                }

                Directory.Delete(pageStorageDir);
            }
        }


        private string GetStorageFilename(GemiUrl url)
        {
            var filename = Path.GetFileName(url.Path);
            return (filename.Length > 0) ? filename : "index.gmi";
        }

        private string GetSavePath(GemiUrl url)
        {
            var dir = GetStorageDirectory(url);
            var file = GetStorageFilename(url);
            return dir + file;
        }

        private string GetStorageDirectory(GemiUrl url)
        {
            string hostDir = (url.Port == 1965) ? url.Hostname : $"{url.Hostname} ({url.Port})";

            string path = Path.GetDirectoryName(url.Path);
            if(string.IsNullOrEmpty(path))
            {
                path = "/";
            }
            if(!path.EndsWith('/'))
            {
                path += "/";
            }

            return $"{pageStorageDir}{hostDir}{path}";
        }

        public bool Store(GemiUrl url, GemiResponse resp)
        {
            if (resp.IsSuccess & resp.HasBody)
            {
                

                var dir = GetStorageDirectory(url);
                var file = GetStorageFilename(url);
                var path = dir + file;

                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception)
                { }

                try
                {
                    //if for some reason the file already exists, don't do anything
                    if (!File.Exists(path))
                    {
                        File.WriteAllBytes(path, resp.BodyBytes);
                        processedCounter.Increment();
                        return true;
                    }
                }
                catch (Exception)
                {
                    failedCounter.Increment();
                    return false;
                }
            }
            return true;
        }

        public override void OutputStatus(string outputFile)
        {
            File.AppendAllText(outputFile, CreateLogLine($"Successully Stored: {processedCounter.Count} Failed: {failedCounter.Count}\n"));
        }

    }
}
