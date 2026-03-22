namespace StocksPlatform.Models;

/// <summary>
/// One persisted sentiment measurement for an asset.
/// Created every time <c>PublicSentimentDeltaService</c> runs against the
/// asset's live news feeds.  The previous reading is used to derive the first
/// derivative (rate of change) of the sentiment signal.
/// </summary>
public class SentimentReading
{
    public int      Id               { get; set; }
    public Guid     AssetId          { get; set; }
    public DateTime Timestamp        { get; set; }   // UTC

    /// <summary>How many words were matched against the financial word list.</summary>
    public int    WordsAnalyzed    { get; set; }

    /// <summary>Mean score of all matched words; range roughly [-1, +1].</summary>
    public double AverageWordScore { get; set; }
}
