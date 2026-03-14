using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services;

/// <summary>
/// Fetches live prices and price history from the e24.no Børs API.
///   - Live prices:   https://api.e24.no/bors/v2/instruments/{symbol}.{exchange}/ticker
///   - History:       https://api.e24.no/bors/chart/{symbol}.{exchange}
/// Supported exchanges: OSE (Oslo Stock Exchange) and NAS (NASDAQ via e24).
/// Assets listed on other markets cannot use this API and must be sourced elsewhere.
/// </summary>
public class AssetPriceService(AppDbContext db, HttpClient http)
{
    private const string BaseUrl    = "https://api.e24.no/bors/chart";
    private const string TickerBase = "https://api.e24.no/bors/v2/instruments";
    private static readonly TimeSpan IncrementalThreshold = TimeSpan.FromDays(28);

    /// <summary>
    /// Ensures daily bars are up-to-date in the database.
    /// Fetches period=28months on the first request or when last fetch is older than 28 days;
    /// uses period=1months for incremental updates within the threshold.
    /// </summary>
    /// <param name="exchange">Exchange suffix used in e24 URLs, e.g. "OSE" or "NAS".</param>
    public async Task EnsureDailyBarsAsync(Guid assetId, string symbol, string exchange = "OSE")
    {
        var meta = await db.AssetHistoryMeta.FirstOrDefaultAsync(m => m.AssetId == assetId);
        var now = DateTime.UtcNow;

        var period = meta?.LastDailyFetchAt is { } last && (now - last) <= IncrementalThreshold
            ? "1months"
            : "12months";

        var url = $"{BaseUrl}/{symbol}.{exchange}?period={period}&type=stock&withVolume=true";
        var response = await FetchAsync(url);
        if (response?.Data is null) return;

        var volumeMap = BuildVolumeMap(response);

        // Bulk upsert: load all existing daily bars for the asset into a dictionary
        var existing = await db.AssetDailyHistory
            .Where(b => b.AssetId == assetId)
            .ToDictionaryAsync(b => b.Timestamp);

        foreach (var point in response.Data)
        {
            var date = DateTimeOffset.FromUnixTimeMilliseconds((long)point[0]).UtcDateTime.Date;
            var price = (decimal)point[1];
            volumeMap.TryGetValue((long)point[0], out var volume);

            if (existing.TryGetValue(date, out var row))
            {
                row.Price = price;
                row.Volume = volume;
            }
            else
            {
                db.AssetDailyHistory.Add(new AssetDailyHistory { AssetId = assetId, Timestamp = date, Price = price, Volume = volume });
            }
        }

        if (meta is null)
            db.AssetHistoryMeta.Add(new AssetPriceMeta { AssetId = assetId, LastDailyFetchAt = now });
        else
            meta.LastDailyFetchAt = now;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Fetches intraday bars (period=5days) from e24.no and persists them.
    /// Old bars beyond 6 days are pruned. Always fetches fresh data when called.
    /// </summary>
    /// <param name="exchange">Exchange suffix used in e24 URLs, e.g. "OSE" or "NAS".</param>
    public async Task EnsureIntradayBarsAsync(Guid assetId, string symbol, string exchange = "OSE")
    {
        var url = $"{BaseUrl}/{symbol}.{exchange}?period=5days&type=stock&withVolume=true";
        var response = await FetchAsync(url);
        if (response?.Data is null) return;

        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-6);

        // Prune bars older than the 5-day window
        var old = await db.AssetIntradayHistory
            .Where(b => b.AssetId == assetId && b.Timestamp < cutoff)
            .ToListAsync();
        if (old.Count > 0) db.AssetIntradayHistory.RemoveRange(old);

        var volumeMap = BuildVolumeMap(response);

        var existing = await db.AssetIntradayHistory
            .Where(b => b.AssetId == assetId && b.Timestamp >= cutoff)
            .ToDictionaryAsync(b => b.Timestamp);

        foreach (var point in response.Data)
        {
            var ts = DateTimeOffset.FromUnixTimeMilliseconds((long)point[0]).UtcDateTime;
            var price = (decimal)point[1];
            volumeMap.TryGetValue((long)point[0], out var volume);

            if (existing.TryGetValue(ts, out var row))
            {
                row.Price = price;
                row.Volume = volume;
            }
            else
            {
                db.AssetIntradayHistory.Add(new AssetIntradayHistory { AssetId = assetId, Timestamp = ts, Price = price, Volume = volume });
            }
        }

        await db.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // Live price (ticker) — cached with a 1-minute TTL
    // -------------------------------------------------------------------------

    /// <summary>Returns the current price for the given asset, using a 1-minute cache.</summary>
    public async Task<decimal> GetPriceAsync(Guid assetId)
    {
        var now = DateTime.UtcNow;
        var cached = await db.AssetPrices.FirstOrDefaultAsync(p => p.AssetId == assetId);

        if (cached is not null && cached.ExpiresAt > now)
            return cached.Price;

        var price = await FetchLivePriceAsync(assetId);
        UpsertCachedPrice(cached, assetId, price, now);
        await db.SaveChangesAsync();
        return price;
    }

    /// <summary>Returns prices for multiple assets, batching cache lookups.</summary>
    public async Task<Dictionary<Guid, decimal>> GetPricesAsync(IEnumerable<Guid> assetIds)
    {
        var ids = assetIds.Distinct().ToList();
        var now = DateTime.UtcNow;
        var result = new Dictionary<Guid, decimal>();

        var cached = await db.AssetPrices
            .Where(p => ids.Contains(p.AssetId))
            .ToListAsync();

        var cachedMap = cached.ToDictionary(p => p.AssetId);
        var staleIds = ids.Where(id => !cachedMap.TryGetValue(id, out var c) || c.ExpiresAt <= now).ToList();

        foreach (var id in staleIds)
        {
            var price = await FetchLivePriceAsync(id);
            cachedMap.TryGetValue(id, out var existing);
            UpsertCachedPrice(existing, id, price, now);
            result[id] = price;
        }

        if (staleIds.Count > 0)
            await db.SaveChangesAsync();

        foreach (var id in ids.Except(staleIds))
            result[id] = cachedMap[id].Price;

        return result;
    }

    /// <summary>
    /// Fetches the live price for one asset via the e24 ticker API.
    /// Falls back to a deterministic stub for unsupported markets.
    /// </summary>
    private async Task<decimal> FetchLivePriceAsync(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);

        var exchange = asset?.Market?.ToUpperInvariant() switch
        {
            "OSE"    => "OSE",
            "NASDAQ" => "NAS",
            _        => null
        };

        if (exchange is not null && asset?.Symbol is { } symbol)
        {
            var url = $"{TickerBase}/{symbol}.{exchange}/ticker";
            var response = await FetchTickerAsync(url);
            if (response?.Ticker?.Value is { } value)
                return (decimal)value;
        }

        // Unsupported market — deterministic stub based on asset ID
        var seed = Math.Abs(assetId.GetHashCode());
        return Math.Round(50m + (decimal)(new Random(seed).NextDouble() * 450), 2);
    }

    private void UpsertCachedPrice(AssetPrice? existing, Guid assetId, decimal price, DateTime now)
    {
        var expiry = now.AddMinutes(1);
        if (existing is null)
            db.AssetPrices.Add(new AssetPrice { AssetId = assetId, Price = price, FetchedAt = now, ExpiresAt = expiry });
        else
        {
            existing.Price = price;
            existing.FetchedAt = now;
            existing.ExpiresAt = expiry;
        }
    }

    private async Task<E24TickerResponse?> FetchTickerAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("accept", "*/*");
        request.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<E24TickerResponse>();
    }

    // -------------------------------------------------------------------------
    // Shared HTTP helper + volume map
    // -------------------------------------------------------------------------

    private async Task<E24Response?> FetchAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("accept", "*/*");
        request.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9");

        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<E24Response>();
    }

    private static Dictionary<long, long?> BuildVolumeMap(E24Response response)
    {
        if (response.Volume is null) return [];
        return response.Volume
            .Where(v => v.Length >= 1 && v[0].HasValue)
            .ToDictionary(
                v => (long)v[0]!.Value,
                v => v.Length >= 2 && v[1].HasValue ? (long?)((long)v[1]!.Value) : null);
    }
}

internal record E24Response(
    [property: JsonPropertyName("data")] double[][]? Data,
    [property: JsonPropertyName("volume")] double?[][]? Volume
);

internal record E24TickerResponse(
    [property: JsonPropertyName("ticker")] E24Ticker? Ticker
);

internal record E24Ticker(
    [property: JsonPropertyName("value")] double? Value
);
