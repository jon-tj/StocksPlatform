using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services.PriceServices;

/// <summary>
/// Fetches price history from the Yahoo Finance chart API:
///   https://query2.finance.yahoo.com/v8/finance/chart/{ticker}?period1=...&period2=...&interval=1d
///
/// Builds the Yahoo ticker by appending an exchange suffix to the asset's Symbol based on its Market
/// (MIC code). E.g. Symbol="ASML" + Market="XAMS" → "ASML.AS".
/// Used as a fallback for markets not covered by E24.
/// </summary>
public class YahooPriceService(AppDbContext db, HttpClient http, ILogger<YahooPriceService> logger) : IAssetPriceProvider
{
    private const string BaseUrl = "https://query1.finance.yahoo.com/v8/finance/chart";
    private static readonly TimeSpan IncrementalThreshold = TimeSpan.FromDays(28);

    /// <summary>Maps MIC exchange codes to Yahoo Finance ticker suffixes.</summary>
    private static readonly Dictionary<string, string> YahooSuffix =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Nordic
            ["XOSL"] = ".OL",   // Oslo
            ["XSTO"] = ".ST",   // Stockholm
            ["XHEL"] = ".HE",   // Helsinki
            ["XCSE"] = ".CO",   // Copenhagen
            // Western Europe
            ["XAMS"] = ".AS",   // Amsterdam
            ["XPAR"] = ".PA",   // Paris
            ["XETR"] = ".DE",   // Frankfurt XETRA
            ["XETA"] = ".DE",   // Frankfurt XETRA (alt)
            ["XETS"] = ".F",   // Frankfurt XETRA (alt)
            ["XLON"] = ".L",    // London
            ["XMIL"] = ".MI",   // Milan
            ["CEUX"] = ".MI",   // Milan (CedX)
            ["XMAD"] = ".MC",   // Madrid
            ["XBRU"] = ".BR",   // Brussels
            ["XLIS"] = ".LS",   // Lisbon
            ["XSWX"] = ".SW",   // Zurich
            ["XVIE"] = ".VI",   // Vienna
            // Eastern Europe
            ["XWAR"] = ".WA",   // Warsaw
            ["XPRA"] = ".PR",   // Prague
            ["XBUD"] = ".BD",   // Budapest
            // North America (no suffix)
            ["XNAS"] = "",
            ["XNYS"] = "",
            ["XNGS"] = "",
            // Other
            ["SSME"] = ".ST",  // Stockholm
            ["XTSE"] = ".TO",   // Toronto
            ["XTSX"] = ".V",   // Vancouver
            ["XTNX"] = ".V",     // Vancouver
            ["CHIX"] = ".XC",     // Swiss Stock Exchange (SIX)
            ["NSME"] = ".ST",      // Stockholm
            ["MERK"] = ".OL"      // Merkur Market (Oslo)
        };

    // -------------------------------------------------------------------------
    // IAssetPriceProvider
    // -------------------------------------------------------------------------

    public async Task EnsureDailyBarsAsync(Guid assetId, string symbol, string? exchange = null)
    {
        var ticker = BuildTicker(symbol, exchange);
        if (ticker is null)
        {
            logger.LogWarning("EnsureDailyBarsAsync: no Yahoo suffix for symbol={Symbol} market={Market}, skipping", symbol, exchange);
            return;
        }

        var meta = await db.AssetHistoryMeta.FirstOrDefaultAsync(m => m.AssetId == assetId);
        var now = DateTime.UtcNow;

        // Full 10-year pull on first fetch or when stale; incremental 1-month otherwise
        var isIncremental = meta?.LastDailyFetchAt is { } last && (now - last) <= IncrementalThreshold;
        var period1 = isIncremental
            ? DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds()
            : DateTimeOffset.UtcNow.AddYears(-10).ToUnixTimeSeconds();
        var period2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        logger.LogInformation("EnsureDailyBarsAsync: fetching {Mode} bars for {Ticker} (assetId={AssetId})",
            isIncremental ? "incremental 1mo" : "full 10yr", ticker, assetId);

        var url = $"{BaseUrl}/{ticker}?period1={period1}&period2={period2}"
                + "&interval=1d&includePrePost=true&events=div%7Csplit%7Cearn&lang=en-US&region=US";
        var chartResult = await FetchChartAsync(url);

        // Persist currency from Yahoo metadata onto the asset (only when it changes).
        if (chartResult?.Meta?.Currency is { Length: > 0 } yahooCurrency)
        {
            var assetToUpdate = await db.Assets.FindAsync(assetId);
            if (assetToUpdate is not null && assetToUpdate.Currency != yahooCurrency)
            {
                assetToUpdate.Currency = yahooCurrency;
                logger.LogInformation("EnsureDailyBarsAsync: setting currency={Currency} for assetId={AssetId}", yahooCurrency, assetId);
            }
        }

        var points = ParseDailyPoints(chartResult);
        if (points is null || points.Length == 0)
        {
            logger.LogWarning("EnsureDailyBarsAsync: Yahoo returned no data for {Ticker}", ticker);
            return;
        }

        logger.LogInformation("EnsureDailyBarsAsync: received {Count} data points for {Ticker}", points.Length, ticker);

        var existing = await db.AssetDailyHistory
            .Where(b => b.AssetId == assetId)
            .ToDictionaryAsync(b => b.Timestamp);

        foreach (var (date, price, volume) in points)
        {
            if (existing.TryGetValue(date, out var row))
            {
                row.Price = price;
                row.Volume = volume;
            }
            else
            {
                db.AssetDailyHistory.Add(new AssetDailyHistory
                { AssetId = assetId, Timestamp = date, Price = price, Volume = volume });
            }
        }

        if (meta is null)
            db.AssetHistoryMeta.Add(new AssetPriceMeta { AssetId = assetId, LastDailyFetchAt = now });
        else
            meta.LastDailyFetchAt = now;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDailyHistoryUniqueViolation(ex) || IsAssetHistoryMetaUniqueViolation(ex))
        {
            // Concurrent requests can race on inserting the same daily rows and/or meta row.
            // Re-read and apply values as updates, then retry once.
            logger.LogWarning(ex,
                "EnsureDailyBarsAsync: unique key race for asset {AssetId}; retrying as update",
                assetId);

            var latestByDate = points
                .GroupBy(p => p.date)
                .ToDictionary(g => g.Key, g => g.Last());

            var dates = latestByDate.Keys.ToArray();
            var freshRows = await db.AssetDailyHistory
                .Where(b => b.AssetId == assetId && dates.Contains(b.Timestamp))
                .ToListAsync();

            foreach (var row in freshRows)
            {
                var latest = latestByDate[row.Timestamp];
                row.Price = latest.price;
                row.Volume = latest.volume;
            }

            // Drop duplicate pending daily inserts that now exist in DB.
            var pendingDaily = db.ChangeTracker.Entries<AssetDailyHistory>()
                .Where(e => e.State == EntityState.Added
                         && e.Entity.AssetId == assetId
                         && latestByDate.ContainsKey(e.Entity.Timestamp))
                .ToList();
            foreach (var e in pendingDaily)
                e.State = EntityState.Detached;

            // Drop duplicate pending meta insert and upsert existing meta row.
            var pendingMeta = db.ChangeTracker.Entries<AssetPriceMeta>()
                .Where(e => e.State == EntityState.Added && e.Entity.AssetId == assetId)
                .ToList();
            foreach (var e in pendingMeta)
                e.State = EntityState.Detached;

            var existingMeta = await db.AssetHistoryMeta.FirstOrDefaultAsync(m => m.AssetId == assetId);
            if (existingMeta is null)
                db.AssetHistoryMeta.Add(new AssetPriceMeta { AssetId = assetId, LastDailyFetchAt = now });
            else
                existingMeta.LastDailyFetchAt = now;

            await db.SaveChangesAsync();
        }
    }

    public async Task EnsureIntradayBarsAsync(Guid assetId, string symbol, string? exchange = null)
    {
        var ticker = BuildTicker(symbol, exchange);
        if (ticker is null) return;

        var period1 = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds();
        var period2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var url = $"{BaseUrl}/{ticker}?period1={period1}&period2={period2}"
                + "&interval=60m&includePrePost=true&events=div%7Csplit%7Cearn&lang=en-US&region=US";

        var result = await FetchChartAsync(url);
        if (result is null) return;

        // Persist currency from Yahoo metadata onto the asset (only when it changes).
        if (result.Meta?.Currency is { Length: > 0 } yahooCurrency)
        {
            var assetToUpdate = await db.Assets.FindAsync(assetId);
            if (assetToUpdate is not null && assetToUpdate.Currency != yahooCurrency)
            {
                assetToUpdate.Currency = yahooCurrency;
                logger.LogInformation("EnsureIntradayBarsAsync: setting currency={Currency} for assetId={AssetId}", yahooCurrency, assetId);
            }
        }

        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-6);

        var old = await db.AssetIntradayHistory
            .Where(b => b.AssetId == assetId && b.Timestamp < cutoff)
            .ToListAsync();
        if (old.Count > 0) db.AssetIntradayHistory.RemoveRange(old);

        var existing = await db.AssetIntradayHistory
            .Where(b => b.AssetId == assetId && b.Timestamp >= cutoff)
            .ToDictionaryAsync(b => b.Timestamp);

        var closes = result.Indicators?.Quote?.FirstOrDefault()?.Close;
        var volumes = result.Indicators?.Quote?.FirstOrDefault()?.Volume;

        for (int i = 0; i < (result.Timestamp?.Length ?? 0); i++)
        {
            var price = closes?[i];
            if (price is null) continue;

            var ts = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp![i]).UtcDateTime;
            if (ts < cutoff) continue;

            var volume = (volumes != null && i < volumes.Length) ? volumes[i] : null;

            if (existing.TryGetValue(ts, out var row))
            {
                row.Price = (decimal)price.Value;
                row.Volume = volume;
            }
            else
            {
                db.AssetIntradayHistory.Add(new AssetIntradayHistory
                { AssetId = assetId, Timestamp = ts, Price = (decimal)price.Value, Volume = volume });
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task<decimal> FetchLivePriceAsync(Guid assetId) =>
        await TryFetchLivePriceAsync(assetId) ?? FallbackPrice(assetId);

    /// <summary>
    /// Returns the live price from Yahoo, or null if the asset is unsupported or the API call fails.
    /// Used by <see cref="AssetPriceService"/> so it can fall back to E24 cleanly.
    /// </summary>
    public async Task<decimal?> TryFetchLivePriceAsync(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset?.Symbol is { } symbol && asset.Market is { } market)
        {
            var ticker = BuildTicker(symbol, market);
            if (ticker is not null)
            {
                var period1 = DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds();
                var period2 = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var url = $"{BaseUrl}/{ticker}?period1={period1}&period2={period2}"
                        + "&interval=1d&includePrePost=true&events=div%7Csplit%7Cearn&lang=en-US&region=US";
                var result = await FetchChartAsync(url);
                if (result?.Meta is { } meta)
                {
                    if (meta.Currency is { Length: > 0 } yahooCurrency && asset.Currency != yahooCurrency)
                    {
                        asset.Currency = yahooCurrency;
                        await db.SaveChangesAsync();
                        logger.LogInformation("TryFetchLivePriceAsync: set currency={Currency} for assetId={AssetId}", yahooCurrency, assetId);
                    }
                    if (meta.RegularMarketPrice is { } livePrice)
                        return (decimal)livePrice;
                }
            }
        }
        return null;
    }

    private decimal FallbackPrice(Guid assetId)
    {
        var seed = Math.Abs(assetId.GetHashCode());
        return Math.Round(50m + (decimal)(new Random(seed).NextDouble() * 450), 2);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>Returns the Yahoo ticker string, or null if the market is unmapped.</summary>
    public static string? BuildTicker(string symbol, string? market)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;
        if (market is null) return symbol;
        if (!YahooSuffix.TryGetValue(market.ToUpperInvariant(), out var suffix)) return null;
        return symbol.Replace(".", "-") + suffix;
    }

    private static (DateTime date, decimal price, long? volume)[]? ParseDailyPoints(YahooChartResult? result)
    {
        if (result is null) return null;

        var closes = result.Indicators?.Quote?.FirstOrDefault()?.Close;
        var volumes = result.Indicators?.Quote?.FirstOrDefault()?.Volume;

        // Deduplicate by date (pre/post-market bars share the same calendar date).
        // Keep the last non-null close for each date.
        var byDate = new Dictionary<DateTime, (decimal price, long? volume)>();
        for (int i = 0; i < (result.Timestamp?.Length ?? 0); i++)
        {
            var price = closes?[i];
            if (price is null) continue;

            var date = DateTimeOffset.FromUnixTimeSeconds(result.Timestamp![i]).UtcDateTime.Date;
            var volume = (volumes != null && i < volumes.Length) ? volumes[i] : null;
            byDate[date] = ((decimal)price.Value, volume);
        }
        return byDate.Select(kv => (kv.Key, kv.Value.price, kv.Value.volume)).ToArray();
    }

    private async Task<YahooChartResult?> FetchChartAsync(string url)
    {
        logger.LogInformation("Yahoo HTTP GET {Url}", url);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("accept", "*/*");
        request.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");
        request.Headers.TryAddWithoutValidation("user-agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Yahoo responded {StatusCode} for {Url}", (int)response.StatusCode, url);
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<YahooChartResponse>();
        return body?.Chart?.Result?.FirstOrDefault();
    }

    private static bool IsDailyHistoryUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqliteException sqliteEx
            && sqliteEx.SqliteErrorCode == 19
            && sqliteEx.Message.Contains("AssetDailyHistory.AssetId, AssetDailyHistory.Timestamp", StringComparison.Ordinal);
    }

    private static bool IsAssetHistoryMetaUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqliteException sqliteEx
            && sqliteEx.SqliteErrorCode == 19
            && sqliteEx.Message.Contains("AssetHistoryMeta.AssetId", StringComparison.Ordinal);
    }
}

internal record YahooChartResponse(
    [property: JsonPropertyName("chart")] YahooChart? Chart
);

internal record YahooChart(
    [property: JsonPropertyName("result")] YahooChartResult[]? Result
);

internal record YahooChartResult(
    [property: JsonPropertyName("meta")] YahooMeta? Meta,
    [property: JsonPropertyName("timestamp")] long[]? Timestamp,
    [property: JsonPropertyName("indicators")] YahooIndicators? Indicators
);

internal record YahooMeta(
    [property: JsonPropertyName("regularMarketPrice")] double? RegularMarketPrice,
    [property: JsonPropertyName("currency")] string? Currency
);

internal record YahooIndicators(
    [property: JsonPropertyName("quote")] YahooQuote[]? Quote
);

internal record YahooQuote(
    [property: JsonPropertyName("close")] double?[]? Close,
    [property: JsonPropertyName("volume")] long?[]? Volume
);

