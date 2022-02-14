using System;
using System.IO;

using Kennedy.Crawler.Utils;


namespace Kennedy.Crawler.Modules
{
    public abstract class AbstractModule
    {
        public string Name { get; private set; }

        public string LogFilename { get; set; }

        public AbstractModule(string name)
        {
            Name = name;
            processedCounter = new ThreadSafeCounter();
        }

        protected ThreadSafeCounter processedCounter;

        protected string CreatePrefix()
            => $"{DateTime.Now}\t{Name}\t";

        protected abstract string GetStatusMesssage();

        public void OutputStatus()
        {
            if (!String.IsNullOrEmpty(LogFilename))
            {
                File.AppendAllText(LogFilename, $"{CreatePrefix()}{GetStatusMesssage()}\n");
            }
        }
        
    }
}
