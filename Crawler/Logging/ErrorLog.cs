using System.IO;

namespace Kennedy.Blazer.Logging
{
    public class ErrorLog
    {
        string OutputFile;

        public ErrorLog(string outputFile)
        {
            OutputFile = outputFile;
        }

        public void LogError(string msg, string url)
        {
            File.AppendAllText(OutputFile, $"{DateTime.Now}\tError: {msg} on '{url}'{Environment.NewLine}");
        }
    }
}
