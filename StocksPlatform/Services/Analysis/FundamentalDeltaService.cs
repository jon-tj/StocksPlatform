using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services.PriceServices;

namespace StocksPlatform.Services.Analysis;

/// <summary>
/// Computes <c>FundamentalDelta</c> for a stock by fetching financial timeseries data
/// from Yahoo Finance and applying three complementary intrinsic-value models:
///
/// <list type="bullet">
///   <item><b>Benjamin Graham</b> – V* = EPS × (8.5 + 2g) × (4.4 / Y)</item>
///   <item><b>Two-stage DCF</b>   – PV of 5-year projected EPS + Gordon-growth terminal value</item>
///   <item><b>Earnings Multiple</b> – EPS × mean P/E of the 5 most-correlated peer stocks
///         (correlation.json); peer P/E is first attempted from the cached
///         <see cref="FundamentalSnapshot"/> table before fetching from Yahoo.</item>
/// </list>
///
/// The consensus value is the average of available estimates.
/// Delta = clamp(upside × 2, −1, 1) where upside = consensusValue / currentPrice − 1.
///
/// Results are persisted as <see cref="FundamentalSnapshot"/> rows; subsequent calls within
/// the same UTC day return the cached delta without any network requests.
/// Caller is responsible for <c>SaveChangesAsync</c>.
/// </summary>
public class FundamentalDeltaService(
    HttpClient httpClient,
    AppDbContext db,
    OnnxPriceModelRegistry correlationRegistry,
    ILogger<FundamentalDeltaService> logger)
{
    // ── Yahoo Finance URL template ────────────────────────────────────────────

    private const string TimeseriesUrlTemplate =
        "https://query1.finance.yahoo.com/ws/fundamentals-timeseries/v1/finance/timeseries/{0}" +
        "?merge=false&padTimeSeries=false&period1=493590046&period2=1774213199" +
        "&type={1}&lang=en-US&region=US";

    private const string FullTypes =
        "trailingDilutedEPS,trailingNormalizedDilutedEPS,trailingTotalRevenue," +
        "annualTotalRevenue,trailingEBITDA,trailingOperatingIncome,trailingNetIncome," +
        "annualDilutedEPS,trailingDividendPerShare";

    private const string FullTypesEncoded =
        "trailingDilutedEPS%2CtrailingNormalizedDilutedEPS%2CtrailingTotalRevenue%2C" +
        "annualTotalRevenue%2CtrailingEBITDA%2CtrailingOperatingIncome%2CtrailingNetIncome%2C" +
        "annualDilutedEPS%2CtrailingDividendPerShare";

    private const string EpsOnlyType = "trailingDilutedEPS";
    private const double DefaultGrowthRate = 0.05;   // 5 %

    // Graham: V* = EPS × (8.5 + 2g%) × (4.4 / Y%)
    private const double GrahamBaseMultiple = 8.5;
    private const double AaaBondYieldPct    = 4.5;   // hardcoded current AAA yield

    // DCF
    private const double Wacc          = 0.09;   // 9 % discount rate
    private const double TerminalGrowth = 0.025;  // 2.5 % terminal growth
    private const int    DcfStageYears  = 5;

    // P/E sanity bounds (excludes extreme / negative ratios)
    private const double MinReasonablePe = 3.0;
    private const double MaxReasonablePe = 100.0;

    // Used when fewer than 2 peers have valid P/E data
    private const double FallbackPe = 15.0;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns today's FundamentalDelta for <paramref name="assetId"/>, computing
    /// and persisting a new <see cref="FundamentalSnapshot"/> when no same-day cache exists.
    /// Returns 0.0 (neutral) when financial data is unavailable.
    /// </summary>
    public async Task<double> ComputeAsync(Guid assetId)
    {
        var today = DateTime.UtcNow.Date;

        var cached = await db.FundamentalSnapshots
            .Where(s => s.AssetId == assetId && s.Date == today)
            .FirstOrDefaultAsync();
        if (cached is not null)
            return cached.FundamentalDelta;

        var asset = await db.Assets.FindAsync(assetId);
        if (asset?.Symbol is null)
            return 0.0;

        // Prune stale snapshots (keep 30 days for historical reference)
        await db.FundamentalSnapshots
            .Where(s => s.AssetId == assetId && s.Date < today.AddDays(-30))
            .ExecuteDeleteAsync();

        return await ComputeAndPersistAsync(asset, today);
    }

    /// <summary>Returns the most-recently persisted snapshot for <paramref name="assetId"/>.</summary>
    public Task<FundamentalSnapshot?> GetLatestSnapshotAsync(Guid assetId) =>
        db.FundamentalSnapshots
            .Where(s => s.AssetId == assetId)
            .OrderByDescending(s => s.Date)
            .FirstOrDefaultAsync();

    // ── Core computation ──────────────────────────────────────────────────────

    private async Task<double> ComputeAndPersistAsync(Asset asset, DateTime date)
    {
        var ticker = YahooPriceService.BuildTicker(asset.Symbol!, asset.Market) ?? asset.Symbol!;

        // ── 1. Fetch primary financial data ───────────────────────────────────
        var doc = await FetchTimeseriesAsync(ticker, FullTypesEncoded);
        if (doc is null)
        {
            logger.LogWarning("FundamentalDelta: no Yahoo timeseries data for {Ticker}", ticker);
            return 0.0;
        }

        // ── 2. Extract raw numbers ────────────────────────────────────────────
        var trailingEps     = ExtractLatestValue(doc, "trailingDilutedEPS");
        var normalizedEps   = ExtractLatestValue(doc, "trailingNormalizedDilutedEPS");
        var trailingRevenue = ExtractLatestValue(doc, "trailingTotalRevenue");
        var trailingEbitda  = ExtractLatestValue(doc, "trailingEBITDA");
        var operatingIncome = ExtractLatestValue(doc, "trailingOperatingIncome");
        var netIncome       = ExtractLatestValue(doc, "trailingNetIncome");
        var dividendPerShare = ExtractLatestValue(doc, "trailingDividendPerShare");
        var annualRevenues  = ExtractAnnualValues(doc, "annualTotalRevenue");
        var annualEps       = ExtractAnnualValues(doc, "annualDilutedEPS");

        // Prefer normalized EPS for valuation; fall back to raw trailing
        var eps = normalizedEps ?? trailingEps;

        // ── 3. Growth rate from multi-year data ───────────────────────────────
        // Prefer EPS CAGR (more correct for Graham's g), fall back to revenue CAGR,
        // then fall back to a conservative default so Graham always computes.
        var growthRate = EstimateGrowthRate(annualEps)
                      ?? EstimateGrowthRate(annualRevenues)
                      ?? DefaultGrowthRate;

        // ── 4. Current price from DB ──────────────────────────────────────────
        var currentPrice = await GetCurrentPriceAsync(asset.Id);

        // ── 5. Graham formula ─────────────────────────────────────────────────
        // V* = EPS × (8.5 + 2g%) × (4.4 / Y)
        // g is the expected 7–10 year EPS growth rate in percentage points.
        // growthRate is never null here (fallback applied above).
        double? grahamValue = null;
        if (eps is > 0)
        {
            var gPct = growthRate * 100;   // fraction → percentage points
            var v    = eps.Value * (GrahamBaseMultiple + 2 * gPct) * (4.4 / AaaBondYieldPct);
            grahamValue = v > 0 ? v : null;
        }

        // ── 6. Two-stage DCF ──────────────────────────────────────────────────
        double? dcfValue = null;
        if (eps is > 0)
        {
            var g = Math.Min(growthRate, 0.20);
            dcfValue = ComputeDcf(eps.Value, g, Wacc, TerminalGrowth, DcfStageYears);
            if (dcfValue <= 0) dcfValue = null;
        }

        // ── 7. Earnings multiple (peer P/E) ───────────────────────────────────
        var (peerMeanPe, usedPeerSymbols) = await ComputePeerPeAsync(asset.Symbol!);

        double? earningsMultiple = null;
        double? assetPe          = null;

        if (eps is > 0)
        {
            if (peerMeanPe.HasValue)
                earningsMultiple = eps.Value * peerMeanPe.Value;

            if (currentPrice.HasValue && currentPrice.Value > 0)
            {
                var rawPe = currentPrice.Value / eps.Value;
                assetPe = (rawPe is >= MinReasonablePe and <= MaxReasonablePe * 5) ? rawPe : null;
            }
        }

        // ── 8. Consensus ─────────────────────────────────────────────────────
        var estimates = new List<double>();
        if (grahamValue.HasValue)        estimates.Add(grahamValue.Value);
        if (dcfValue.HasValue)           estimates.Add(dcfValue.Value);
        if (earningsMultiple.HasValue)   estimates.Add(earningsMultiple.Value);
        double? consensusValue = estimates.Count > 0 ? estimates.Average() : null;

        // ── 9. Delta ──────────────────────────────────────────────────────────
        double delta = 0.0;
        if (consensusValue.HasValue && currentPrice is > 0)
        {
            var upside = consensusValue.Value / currentPrice.Value - 1.0;
            delta = Math.Max(-1.0, Math.Min(1.0, upside * 2.0));
        }

        // ── 10. Persist ───────────────────────────────────────────────────────
        var snapshot = new FundamentalSnapshot
        {
            AssetId                = asset.Id,
            Date                   = date,
            TrailingEps            = trailingEps,
            NormalizedEps          = normalizedEps,
            TrailingRevenue        = trailingRevenue,
            TrailingEbitda         = trailingEbitda,
            TrailingOperatingIncome = operatingIncome,
            TrailingNetIncome      = netIncome,
            TrailingDividendPerShare = dividendPerShare,
            RevenueGrowthRate      = growthRate,
            CurrentPrice           = currentPrice,
            GrahamValue            = grahamValue,
            DcfValue               = dcfValue,
            EarningsMultipleValue  = earningsMultiple,
            ConsensusValue         = consensusValue,
            PeerMeanPe             = peerMeanPe,
            PeerSymbols            = usedPeerSymbols is { Count: > 0 }
                                         ? JsonSerializer.Serialize(usedPeerSymbols)
                                         : null,
            AssetCurrentPe         = assetPe,
            FundamentalDelta       = delta,
        };

        db.FundamentalSnapshots.Add(snapshot);

        logger.LogInformation(
            "FundamentalDelta {Symbol}: EPS={Eps:0.##} | Graham={G:0.##} | DCF={D:0.##} | " +
            "EM={E:0.##} | Consensus={C:0.##} | Price={P:0.##} | PeerPE={PP:0.#} | Growth={Gr:P1} | Delta={Delta:0.###}",
            asset.Symbol, eps, grahamValue, dcfValue, earningsMultiple,
            consensusValue, currentPrice, peerMeanPe, growthRate, delta);

        return delta;
    }

    // ── Valuation helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Two-stage DCF: discounted projected EPS for <paramref name="stageYears"/> years
    /// at growth rate <paramref name="g"/>, plus a Gordon-growth terminal value.
    /// </summary>
    private static double ComputeDcf(double eps, double g, double wacc, double gTerminal, int stageYears)
    {
        double pv = 0;
        double projected = eps;
        for (int n = 1; n <= stageYears; n++)
        {
            projected *= (1 + g);
            pv += projected / Math.Pow(1 + wacc, n);
        }
        var terminalValue = projected * (1 + gTerminal) / (wacc - gTerminal);
        pv += terminalValue / Math.Pow(1 + wacc, stageYears);
        return pv;
    }

    // ── Peer P/E ──────────────────────────────────────────────────────────────

    private async Task<(double? MeanPe, List<string>? UsedSymbols)> ComputePeerPeAsync(string symbol)
    {
        var peers = correlationRegistry.GetTopCorrelated(symbol, topN: 5);
        if (peers.Count == 0)
            return (FallbackPe, null);

        var peerSymbolList = peers.Select(p => p.Symbol).ToList();

        // Assets in our DB that match the peer symbols
        var peerAssets = await db.Assets
            .Where(a => a.Symbol != null && peerSymbolList.Contains(a.Symbol))
            .ToListAsync();

        if (peerAssets.Count == 0)
            return (FallbackPe, peerSymbolList);

        var peerIds = peerAssets.Select(a => a.Id).ToList();

        // Latest cached prices
        var prices = await db.AssetPrices
            .Where(ap => peerIds.Contains(ap.AssetId))
            .ToDictionaryAsync(ap => ap.AssetId, ap => (double)ap.Price);

        // EPS from FundamentalSnapshot cache (≤ 7 days old)
        var cutoff  = DateTime.UtcNow.AddDays(-7);
        var cachedEps = await db.FundamentalSnapshots
            .Where(s => peerIds.Contains(s.AssetId) && s.Date >= cutoff
                     && (s.NormalizedEps != null || s.TrailingEps != null))
            .GroupBy(s => s.AssetId)
            .Select(g => g.OrderByDescending(s => s.Date).First())
            .ToDictionaryAsync(s => s.AssetId, s => s.NormalizedEps ?? s.TrailingEps!.Value);

        var peRatios    = new List<double>();
        var usedSymbols = new List<string>();

        // Fetch peer EPS in parallel for those not in cache
        var fetchTasks = new Dictionary<Guid, Task<double?>>();
        foreach (var peer in peerAssets)
        {
            if (!cachedEps.ContainsKey(peer.Id) && peer.Symbol is not null)
            {
                var peerTicker = YahooPriceService.BuildTicker(peer.Symbol, peer.Market) ?? peer.Symbol;
                fetchTasks[peer.Id] = FetchSingleEpsAsync(peerTicker);
            }
        }
        await Task.WhenAll(fetchTasks.Values);

        foreach (var peer in peerAssets)
        {
            double? epsVal = cachedEps.TryGetValue(peer.Id, out var ce) ? ce
                           : fetchTasks.TryGetValue(peer.Id, out var ft) ? ft.Result
                           : null;

            if (epsVal is null or <= 0) continue;
            if (!prices.TryGetValue(peer.Id, out var price) || price <= 0) continue;

            var pe = price / epsVal.Value;
            if (pe is >= MinReasonablePe and <= MaxReasonablePe)
            {
                peRatios.Add(pe);
                usedSymbols.Add(peer.Symbol!);
            }
        }

        return peRatios.Count >= 2
            ? (peRatios.Average(), usedSymbols)
            : (FallbackPe, peerSymbolList);
    }

    private async Task<double?> FetchSingleEpsAsync(string ticker)
    {
        var doc = await FetchTimeseriesAsync(ticker, EpsOnlyType);
        return doc is null ? null : ExtractLatestValue(doc, "trailingDilutedEPS");
    }

    // ── Yahoo Finance HTTP ────────────────────────────────────────────────────

    private async Task<JsonDocument?> FetchTimeseriesAsync(string ticker, string types)
    {
        try
        {
            var url = string.Format(TimeseriesUrlTemplate,
                Uri.EscapeDataString(ticker), types);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Yahoo timeseries {Status} for {Ticker}",
                    (int)response.StatusCode, ticker);
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Yahoo timeseries for {Ticker}", ticker);
            return null;
        }
    }

    // ── JSON parsing helpers ──────────────────────────────────────────────────

    /// <summary>Extracts the most-recent non-null <c>reportedValue.raw</c> for <paramref name="typeName"/>.</summary>
    private static double? ExtractLatestValue(JsonDocument doc, string typeName)
    {
        if (!doc.RootElement.TryGetProperty("timeseries", out var ts)) return null;
        if (!ts.TryGetProperty("result", out var results)) return null;

        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty(typeName, out var dataArr)) continue;

            JsonElement? lastValid = null;
            foreach (var item in dataArr.EnumerateArray())
                if (item.ValueKind != JsonValueKind.Null) lastValid = item;

            if (lastValid is { } last
                && last.TryGetProperty("reportedValue", out var rv)
                && rv.TryGetProperty("raw", out var raw)
                && raw.TryGetDouble(out var d))
                return d;
        }
        return null;
    }

    /// <summary>Extracts all non-null annual data points for <paramref name="typeName"/>.</summary>
    private static List<(string Date, double Value)> ExtractAnnualValues(JsonDocument doc, string typeName)
    {
        var list = new List<(string, double)>();
        if (!doc.RootElement.TryGetProperty("timeseries", out var ts)) return list;
        if (!ts.TryGetProperty("result", out var results)) return list;

        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty(typeName, out var dataArr)) continue;
            foreach (var item in dataArr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Null) continue;
                if (!item.TryGetProperty("date", out var dateProp)) continue;
                if (!item.TryGetProperty("reportedValue", out var rv)) continue;
                if (!rv.TryGetProperty("raw", out var raw) || !raw.TryGetDouble(out var v)) continue;
                list.Add((dateProp.GetString() ?? "", v));
            }
        }
        return list;
    }

    /// <summary>
    /// Estimates the annual CAGR from multi-year revenue data.
    /// Returns null when fewer than two data points are available.
    /// Result is capped in [−5 %, 30 %].
    /// </summary>
    private static double? EstimateGrowthRate(List<(string Date, double Value)> annualValues)
    {
        if (annualValues.Count < 2) return null;
        // Filter out non-positive values before computing CAGR
        var sorted = annualValues
            .Where(x => x.Value > 0)
            .OrderBy(x => x.Date)
            .ToList();
        if (sorted.Count < 2) return null;
        var first  = sorted.First().Value;
        var last   = sorted.Last().Value;
        var years  = sorted.Count - 1;
        var cagr = Math.Pow(last / first, 1.0 / years) - 1;
        return Math.Max(-0.05, Math.Min(0.30, cagr));
    }

    // ── Price from DB ─────────────────────────────────────────────────────────

    private async Task<double?> GetCurrentPriceAsync(Guid assetId)
    {
        var ap = await db.AssetPrices
            .Where(p => p.AssetId == assetId)
            .FirstOrDefaultAsync();
        if (ap is not null)
            return (double)ap.Price;

        var latest = await db.AssetDailyHistory
            .Where(h => h.AssetId == assetId)
            .OrderByDescending(h => h.Timestamp)
            .FirstOrDefaultAsync();
        return latest is not null ? (double)latest.Price : null;
    }
}
