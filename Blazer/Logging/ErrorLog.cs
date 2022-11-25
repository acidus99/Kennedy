using System;

using Kennedy.Blazer.Utils;

namespace Kennedy.Blazer.Logging
{
    public class ErrorLog
    {
        ThreadedFileWriter errorOut;

        public ErrorLog(string outputDir)
        {
            errorOut = new ThreadedFileWriter(outputDir + "errors.txt", 1);
        }

        public void LogError(string msg, string url)
            => errorOut.WriteLine($"{DateTime.Now}\tError: {msg} on '{url}'");
        
        public void Close()
            => errorOut.Close();
    }
}
