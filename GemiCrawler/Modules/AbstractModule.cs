using System;
namespace GemiCrawler.Modules
{
    public abstract class AbstractModule
    {
        public string Name { get; private set; }

        public AbstractModule(string name)
        {
            Name = name;
        }

        public string CreateLogLine(string msg)
            => $"{DateTime.Now}\t{Name}\t{msg}";
        
    }
}
