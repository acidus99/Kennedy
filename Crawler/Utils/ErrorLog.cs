using System;
namespace Kennedy.Crawler.Utils
{
    public class ErrorLog
    {
        ThreadedFileWriter errorOut;

        public ErrorLog(string outputDir)
        {
            errorOut = new ThreadedFileWriter(outputDir + "errors.txt", 1);
        }

        public void LogError(string msg, string url)
            => errorOut.WriteLine($"{DateTime.Now}\tEXCEPTION: {msg} on '{url}'");
        
        public void Close()
            => errorOut.Close();
    }
}
