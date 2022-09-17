using System;

using NTextCat;

namespace Kennedy.Data.Parsers
{
	public class LanguageDetector
	{
		//minimum size we require the content to be to find out the language
		const int MinSizeForLanguage = 150;

		RankedLanguageIdentifier langClassifier;

		public LanguageDetector(string dataDirectory)
		{
			var factory = new RankedLanguageIdentifierFactory();
			langClassifier = factory.Load(dataDirectory + "Core14.profile.xml");
		}

		public string DetectLanguage(string filteredBody)
		{
			if (filteredBody.Length > MinSizeForLanguage)
			{
				var mostCertainLanguage = langClassifier.Identify(filteredBody).FirstOrDefault();
				return (mostCertainLanguage != null) ? mostCertainLanguage.Item1.Iso639_3 : "";
			}
			return "";
		}


	}
}

