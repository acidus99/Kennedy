using System;
using System.Globalization;

using NTextCat;

namespace Kennedy.Data.Parsers
{
	public class LanguageDetector
	{

		public static string ConfigFileDirectory { get; set; } = "";

		//minimum size we require the content to be to find out the language
		const int MinSizeForLanguage = 150;

		RankedLanguageIdentifier langClassifier;

		public LanguageDetector()
		{
			var factory = new RankedLanguageIdentifierFactory();
			langClassifier = factory.Load(ConfigFileDirectory + "Core14.profile.xml");
		}

		public string? DetectLanguage(string filteredBody)
		{
			if (filteredBody.Length > MinSizeForLanguage)
			{
				var mostCertainLanguage = langClassifier.Identify(filteredBody).FirstOrDefault();
				if(mostCertainLanguage != null)
				{
					CultureInfo info = new CultureInfo(mostCertainLanguage.Item1.Iso639_2T);
					var lang = info.TwoLetterISOLanguageName;
					return lang;
				}
				return null;
			}
			return null;
		}


	}
}

 