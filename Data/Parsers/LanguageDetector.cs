using System;
using System.Globalization;

using NTextCat;

namespace Kennedy.Data.Parsers
{
	public class LanguageDetector
	{
		public static string ConfigFileDirectory { get; set; } = "";

		//minimum size we require the content to be to find out the language
		const int MinSize = 150;

		const int MaxSize = 4096;



		RankedLanguageIdentifier langClassifier;

		public LanguageDetector()
		{
			var factory = new RankedLanguageIdentifierFactory();
			langClassifier = factory.Load(ConfigFileDirectory + "Core14.profile.xml");
		}

		public string? DetectLanguage(string s)
		{
			if (s.Length < MinSize)
			{
				return null;
			}

			//scanning huge amounts of text (10s, 100s or 1000s of KB) is slow and doesn't provide more accuracy. So clip it.
			if(s.Length > MaxSize)
			{
				s = s.Substring(0, MaxSize);
			}

			var mostCertainLanguage = langClassifier.Identify(s).FirstOrDefault();
			if(mostCertainLanguage != null)
			{
				CultureInfo info = new CultureInfo(mostCertainLanguage.Item1.Iso639_2T);
				var lang = info.TwoLetterISOLanguageName;
				return lang;
			}
			return null;
		}
	}
}

 