using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gemi.Net;
using GemiCrawler.Utils;
using System.IO.Compression;

using GemiCrawler.Modules;

namespace GemiCrawler.DocumentStore
{
    /// <summary>
    /// disk-backed KV store, using a directory structure
    /// </summary>
    public class ObjectStore
    {
        string rootDir;

        public ObjectStore(string path)
        {
            rootDir = path;
        }

        private string getPrefixDirectoryForKey(string key)
        {
            if(key.Length < 4)
            {
                return rootDir;
            }
            return rootDir + Path.DirectorySeparatorChar + key[0] + key[1] + Path.DirectorySeparatorChar + key[2] + key[3] + Path.DirectorySeparatorChar;
        }

        public bool StoreObject(string key, byte [] bytes)
        {
            var dir = getPrefixDirectoryForKey(key);
            try
            {
                Directory.CreateDirectory(dir);
                if (!File.Exists(dir + key))
                {
                    File.WriteAllBytes(dir + key, bytes);
                    return true;
                }
            } catch (Exception)
            {

            }
            return false;
        }

        public byte [] GetObject(string key)
        {
            var dir = getPrefixDirectoryForKey(key);
            return File.ReadAllBytes(dir + key);
        }
    }
}
