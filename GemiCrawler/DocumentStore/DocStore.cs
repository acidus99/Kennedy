using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gemi.Net;
using GemiCrawler.Utils;

using GemiCrawler.Modules;

namespace GemiCrawler.DocumentStore
{
    public class DocStore : IDocumentStore
    {

        string pageStorageDir;

        public DocStore(string path)            
        {

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

        public string StoreDocument(GemiUrl url, GemiResponse resp)
        {
            var path = "";

            if (resp.IsSuccess & resp.HasBody)
            {
                var dir = GetStorageDirectory(url);
                var file = GetStorageFilename(url);
                path = dir + file;

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
                    }
                }
                catch (Exception)
                {
                }
            }
            return path;
        }
    }
}
