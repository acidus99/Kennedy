using System;
using GemiCrawler.Utils;


namespace GemiCrawler.Modules
{
    public abstract class AbstractModule
    {
        public string Name { get; private set; }

        public AbstractModule(string name)
        {
            Name = name;
            processedCounter = new ThreadSafeCounter();
        }

        protected ThreadSafeCounter processedCounter;

        public string CreateLogLine(string msg)
            => $"{DateTime.Now}\t{Name}\t{msg}";


        public abstract void OutputStatus(string outputFile);
        
    }
}
