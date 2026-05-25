using System.Globalization;
using NTextCat;

namespace Kennedy.Data.Parsers;

/// <summary>
/// Detects the natural language of a text string using NTextCat's ranked n-gram classifier.
/// Loaded from a language profile file (<c>Core14.profile.xml</c>) that covers 14 common languages.
/// Set <see cref="ConfigFileDirectory"/> before constructing any instance.
/// </summary>
public class LanguageDetector
{
    /// <summary>
    /// Directory containing the NTextCat profile XML file.
    /// Must be set before the first <see cref="LanguageDetector"/> is instantiated (e.g. in Program.cs).
    /// </summary>
    public static string ConfigFileDirectory { get; set; } = "";

    // Content shorter than this threshold doesn't have enough n-gram diversity for reliable detection.
    const int MinSize = 150;

    // Scanning very large texts provides no accuracy gain; cap to keep detection fast.
    const int MaxSize = 4096;

    RankedLanguageIdentifier langClassifier;

    public LanguageDetector()
    {
        var factory = new RankedLanguageIdentifierFactory();
        langClassifier = factory.Load(ConfigFileDirectory + "Core14.profile.xml");
    }

    /// <summary>
    /// Returns the ISO 639-1 two-letter language code for <paramref name="s"/>,
    /// or null when the text is too short to classify reliably.
    /// </summary>
    public string? DetectLanguage(string s)
    {
        if (s.Length < MinSize)
        {
            return null;
        }

        //scanning huge amounts of text (10s, 100s or 1000s of KB) is slow and doesn't provide more accuracy. So clip it.
        if (s.Length > MaxSize)
        {
            s = s.Substring(0, MaxSize);
        }

        var mostCertainLanguage = langClassifier.Identify(s).FirstOrDefault();
        if (mostCertainLanguage != null)
        {
            CultureInfo info = new CultureInfo(mostCertainLanguage.Item1.Iso639_2T);
            var lang = info.TwoLetterISOLanguageName;
            return lang;
        }
        return null;
    }
}