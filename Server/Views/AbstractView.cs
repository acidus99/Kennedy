using System;
using System.IO;
using RocketForce;
namespace Kennedy.Server.Views
{
    internal abstract class AbstractView
    {
        protected Request Request;
        protected Response Response;
        protected App App;

        protected TextWriter Out { get; private set; }

        public AbstractView(Request request, Response response, App app)
        {
            Request = request;
            Response = response;
            App = app;
        }

        public abstract void Render();

        //removes whitepsace so a user query cannot inject new gemtext lines into the output
        protected string SanitizedQuery
            => Request.Url.Query.Replace("\r", "").Replace("\n", "").Trim();


    }
}
