using System.IO;

namespace Kennedy.Blazer.Logging
{
    public class ErrorLog
    {
        string OutputFile;

        public ErrorLog(string outputDir)
        {
            OutputFile = Path.Combine(outputDir, "errors.txt");
        }

        public void LogError(string msg, string url)
        {
            File.WriteAllText(OutputFile, $"{DateTime.Now}\tError: {msg} on '{url}'{Environment.NewLine}");
        }
    }
}
