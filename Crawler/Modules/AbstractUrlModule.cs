using System;
using Gemini.Net;

namespace Kennedy.Crawler.Modules
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

        public abstract bool IsUrlAllowed(GeminiUrl url);

    }
}
