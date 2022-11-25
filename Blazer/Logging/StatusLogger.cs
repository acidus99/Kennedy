using System;
using System.Text.RegularExpressions;

namespace Kennedy.Blazer.Logging
{
	public class StatusLogger
	{
		string OutputDirectory;
		char [] InvalidCharacters;

		public StatusLogger(string outputDirectory)
		{
			OutputDirectory = outputDirectory;
			InvalidCharacters = Path.GetInvalidFileNameChars();
		}

		public void LogStatus(IStatusProvider statusProvider)
		{
			var filename = Path.Combine(OutputDirectory, MakeFilename(statusProvider));

			string msg = $"{DateTime.Now}\t{statusProvider.GetStatus()}{Environment.NewLine}";
			File.AppendAllText(filename, msg);
		}

		private string MakeFilename(IStatusProvider statusProvider)
		{
			var ret = statusProvider.ModuleName;
			foreach(char c in InvalidCharacters)
			{
				ret = ret.Replace(c, '-');
			}
			return ret + ".txt";
        }
	}
}

