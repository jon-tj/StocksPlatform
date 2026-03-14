using System.Globalization;

namespace StocksPlatform.Services;

public class PollWeekService
{
    /// <summary>
    /// Returns the week code for the current UTC date, e.g. "2612" = week 12 of 2026.
    /// Uses ISO 8601 week numbering (Monday = first day).
    /// </summary>
    public string GetCurrentPollId()
    {
        var now = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(now);
        var year = ISOWeek.GetYear(now) % 100;
        return $"{year:D2}{week:D2}";
    }
}
