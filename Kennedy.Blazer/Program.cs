using System;

using Gemini.Net;

namespace Kennedy.Blazer
{
    class Program
    {
        static void Main(string[] args)
        {

            var checker = new RobotsChecker();


            var result = checker.IsAllowed("gemini://gemi.dev/gemlog/");

            result = checker.IsAllowed("gemini://gemi.dev/cgi-bin/wp.cgi/view?Cat");


        }
    }
}