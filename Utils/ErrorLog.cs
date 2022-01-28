using System;
namespace Gemini.Net.Crawler.Utils
{
    public class ErrorLog
    {
        ThreadedFileWriter errorOut;

        public ErrorLog(string outputDir)
        {
            errorOut = new ThreadedFileWriter(outputDir + "errors.txt", 1);
        }

        public void LogError(Exception ex, string url)
            => errorOut.WriteLine($"{DateTime.Now}\tEXCEPTION: {ex.Message} on '{url}'");
        
        public void Close()
            => errorOut.Close();
    }
}
