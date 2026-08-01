using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Kennedy.SearchIndex.Utils;

internal static class FaviconValidator
{
    public static bool IsValidEmoji(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        // A favicon is one displayed emoji. This accepts ZWJ sequences and flags,
        // but rejects multiple adjacent emoji and ordinary short text.
        var textElements = StringInfo.GetTextElementEnumerator(value);
        if (!textElements.MoveNext() || textElements.MoveNext())
        {
            return false;
        }

        if (IsKeycap(value))
        {
            return true;
        }

        bool containsEmoji = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (IsEmojiBase(rune.Value))
            {
                containsEmoji = true;
            }
            else if (!IsEmojiComponent(rune.Value))
            {
                return false;
            }
        }

        return containsEmoji;
    }

    private static bool IsKeycap(string value)
        => value.Length >= 2 &&
           (value[0] is >= '0' and <= '9' or '#' or '*') &&
           value.EndsWith("\u20E3", StringComparison.Ordinal) &&
           value.Skip(1).All(c => c is '\uFE0F' or '\u20E3');

    private static bool IsEmojiBase(int value)
        => (value >= 0x1F000 && value <= 0x1FAFF) ||
           (value >= 0x2600 && value <= 0x27BF) ||
           value is 0x00A9 or 0x00AE or 0x203C or 0x2049 or 0x2122 or 0x2139 or
               0x231A or 0x231B or 0x2328 or 0x23CF or 0x24C2 or 0x25B6 or 0x25C0 or
               0x2B50 or 0x2B55 or 0x3030 or 0x303D or 0x3297 or 0x3299 ||
           (value >= 0x23E9 && value <= 0x23F3) ||
           (value >= 0x23F8 && value <= 0x23FA) ||
           (value >= 0x25AA && value <= 0x25AB) ||
           (value >= 0x25FB && value <= 0x25FE) ||
           (value >= 0x2934 && value <= 0x2935) ||
           (value >= 0x2B05 && value <= 0x2B07) ||
           (value >= 0x2B1B && value <= 0x2B1C);

    private static bool IsEmojiComponent(int value)
        => value is 0x200D or 0xFE0F or 0x20E3 ||
           value is >= 0xE0020 and <= 0xE007F;
}
