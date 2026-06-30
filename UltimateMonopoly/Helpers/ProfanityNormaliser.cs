using System.Globalization;
using System.Text;

namespace UltimateMonopoly.Helpers;

/// <summary>
/// Canonicalises a string for profanity matching. The SAME transform is applied to both the seed words
/// (stored as <c>BlockedWord.NormalisedWord</c>) and to user input before matching, so the two sides are
/// always comparable — uppercasing is just its final step.
/// </summary>
public static class ProfanityNormaliser
{
    // Common leetspeak / symbol substitutions → the letter they stand in for. Applied after uppercasing.
    private static readonly Dictionary<char, char> LeetMap = new()
    {
        ['0'] = 'O', ['1'] = 'I', ['3'] = 'E', ['4'] = 'A', ['5'] = 'S',
        ['7'] = 'T', ['8'] = 'B', ['9'] = 'G', ['@'] = 'A', ['$'] = 'S',
        ['!'] = 'I', ['|'] = 'I', ['+'] = 'T', ['('] = 'C',
    };

    /// <summary>
    /// Returns the canonical form: strip diacritics (é→e), map leetspeak, drop every non-letter (so
    /// separators / punctuation / whitespace vanish — defeating "f.u.c.k" and "f u c k"), then collapse
    /// runs of 3+ identical letters to one ("fuuuuck"→"fuck"). No English word has 3 identical letters in
    /// a row, so that collapse never mangles a real word, while genuine doubles ("bookkeeper") survive.
    /// </summary>
    public static string Normalise(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // 1. Decompose accented chars so each diacritic becomes a separate, droppable mark.
        var decomposed = input.Normalize(NormalizationForm.FormD);

        // 2. Uppercase, leet-map, keep letters only.
        var cleaned = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue; // diacritic

            var upper = char.ToUpperInvariant(ch);
            if (LeetMap.TryGetValue(upper, out var mapped))
                upper = mapped;

            if (char.IsLetter(upper))
                cleaned.Append(upper);
        }

        // 3. Collapse runs of 3+ identical letters to a single one.
        var s = cleaned.ToString();
        if (s.Length == 0)
            return s;

        var result = new StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            var j = i;
            while (j < s.Length && s[j] == c)
                j++;

            var runLength = j - i;
            result.Append(c, runLength >= 3 ? 1 : runLength);
            i = j;
        }

        return result.ToString();
    }
}