using System;
using Gemini.Net

namespace GemiCrawler.Modules
{
    /// <summary>
    /// Abstract module that determines if a URL is allowed to be added to the Url Frontier
    /// </summary>
    public abstract class AbstractUrlModule : AbstractModule
    {
        public AbstractUrlModule(string name)
            :base(name)
        {
        }

        public abstract bool IsUrlAllowed(GemiUrl url);

    }
}
