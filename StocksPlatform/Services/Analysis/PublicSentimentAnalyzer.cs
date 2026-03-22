namespace StocksPlatform.Services.Analysis;

/// <summary>
/// Runs simplistic lexicon-based sentiment analysis over a sequence of text
/// strings using <see cref="FinancialWordScores"/>.
///
/// Pre-processing steps applied to each text:
///   1. Replace every non-letter character with a space.
///   2. Lower-case every letter.
///   3. Split on whitespace and discard tokens shorter than 3 characters.
///
/// Only tokens that match a key in <see cref="FinancialWordScores.Scores"/>
/// are counted; unrecognized tokens are silently skipped.
/// </summary>
public static class PublicSentimentAnalyzer
{
    /// <summary>
    /// Analyzes <paramref name="texts"/> and returns
    /// (<c>WordsAnalyzed</c>, <c>AverageWordScore</c>).
    /// If no matching words are found both values are 0.
    /// </summary>
    public static (int WordsAnalyzed, double AverageWordScore) Analyze(IEnumerable<string> texts)
    {
        var total = 0.0;
        var count = 0;

        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            foreach (var word in Tokenize(text))
            {
                if (FinancialWordScores.Scores.TryGetValue(word, out var score))
                {
                    total += score;
                    count++;
                }
            }
        }

        return (count, count == 0 ? 0.0 : total / count);
    }

    /// <summary>
    /// Converts <paramref name="text"/> to lowercase alphabetic tokens of
    /// length ≥ 3. All punctuation, digits, and whitespace act as delimiters.
    /// </summary>
    private static IEnumerable<string> Tokenize(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
            sb.Append(char.IsLetter(c) ? char.ToLowerInvariant(c) : ' ');

        return sb.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3);
    }
}
