using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services.CompanyNews;

namespace StocksPlatform.Services.Analysis;

/// <summary>
/// Computes <c>PublicSentimentDelta</c> for a given asset by:
/// <list type="number">
///   <item>Fetching all available public news (Nordnet, E24, company feed) in parallel.</item>
///   <item>Running <see cref="PublicSentimentAnalyzer"/> on the combined text.</item>
///   <item>
///     Loading the most recent <see cref="SentimentReading"/> to compute the
///     first derivative (change in average score since last measurement).
///     Defaults to 0 when no previous reading exists.
///   </item>
///   <item>
///     Mapping <c>level + derivative</c> to the [-1, 1] delta range (neutral = 0.0)
///     using the formula:<br/>
///     <c>delta = clamp(level × 0.7 + derivative × 0.3, -1, 1)</c>
///   </item>
///   <item>
///     Staging a new <see cref="SentimentReading"/> on the <see cref="AppDbContext"/>
///     (the caller — <c>AnalysisService</c> — is responsible for <c>SaveChangesAsync</c>).
///   </item>
/// </list>
/// Returns 1.0 (neutral) when no words could be matched or on any fetch error.
/// </summary>
public class PublicSentimentDeltaService(
    AppDbContext db,
    PublicSentimentService publicSentiment,
    CompanyNewsFeedService companyNewsFeed,
    ILogger<PublicSentimentDeltaService> logger)
{
    // The current sentiment level carries more weight than the trend.
    private const double LevelWeight      = 0.7;
    private const double DerivativeWeight = 0.3;

    public async Task<double> ComputeAsync(Guid assetId, DateTime date)
    {
        try
        {
            var asset = await db.Assets.FindAsync(assetId);
            if (asset is null) return 1.0;

            var items = await GatherItemsAsync(asset);
            var texts = items.Select(i => i.Title + " " + i.Body);
            var (wordsAnalyzed, avgScore) = PublicSentimentAnalyzer.Analyze(texts);

            if (wordsAnalyzed == 0) return 1.0;

            // First derivative: change since the previous measurement.
            var prev = await db.SentimentReadings
                .Where(r => r.AssetId == assetId)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefaultAsync();

            double derivative = prev is null ? 0.0 : avgScore - prev.AverageWordScore;

            // Stage new reading — SaveChangesAsync is handled by AnalysisService.
            db.SentimentReadings.Add(new SentimentReading
            {
                AssetId          = assetId,
                Timestamp        = DateTime.UtcNow,
                WordsAnalyzed    = wordsAnalyzed,
                AverageWordScore = avgScore,
            });

            var delta = avgScore * LevelWeight + derivative * DerivativeWeight;
            return Math.Clamp(delta, -1.0, 1.0);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to compute public sentiment delta for {AssetId}", assetId);
            return 0.0;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<List<SentimentItem>> GatherItemsAsync(Asset asset)
    {
        var tasks = new List<Task<List<SentimentItem>>>();

        var nordnetSlug = asset.Broker?.ToLowerInvariant().Contains("nordnet") == true
            ? asset.BrokerSymbol : null;

        if (nordnetSlug is not null)
            tasks.Add(FlattenNordnetAsync(nordnetSlug));

        if (asset.E24Tag is not null)
            tasks.Add(publicSentiment.GetE24NewsAsync(asset.E24Tag, limit: 20));

        if (companyNewsFeed.Supports(asset.Symbol, asset.Market))
            tasks.Add(companyNewsFeed.FetchAsync(asset.Symbol!, asset.Market!, limit: 20));

        if (tasks.Count == 0) return [];

        await Task.WhenAll(tasks);

        return tasks
            .Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .ToList();
    }

    private async Task<List<SentimentItem>> FlattenNordnetAsync(string slug)
    {
        var (comments, news) = await publicSentiment.GetNordnetAsync(slug);
        return [.. comments, .. news];
    }
}
