using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;
using StocksPlatform.Services.FundServices;

namespace StocksPlatform.Services;

/// <summary>
/// Aggregates institutional fund holdings across all registered IFundHoldingsProvider
/// implementations and computes per-asset InstitutionalOrderFlowDelta scores.
///
/// Algorithm:
///   1. Daily (idempotent): each provider fetches its funds' current holdings.
///   2. Per-fund: compare the portfolio date against the stored FundPortfolioMeta.
///   3. If any fund has a new portfolio date, re-aggregate all holdings into a
///      FundHoldingSnapshot row (date, assetId, mean%, median%, numFunds).
///   4. InstitutionalOrderFlowDelta = clamp((today_mean - yesterday_mean) / 100, -1, 1).
///      · -1  → holding dropped by 100 pp across funds (e.g. fully sold)
///      ·  0  → no change in institutional representation
///      · +1  → holding increased by 100 pp (e.g. newly 100 % of a fund)
/// </summary>
public class FundInstitutionalService(AppDbContext db, IEnumerable<IFundHoldingsProvider> providers)
{
    private readonly IReadOnlyList<IFundHoldingsProvider> _providers = providers.ToList();

    /// <summary>
    /// Idempotent daily refresh. Calls every registered IFundHoldingsProvider, detects
    /// whether any fund's portfolio has been updated since the last run, and — if so —
    /// writes a FundHoldingSnapshot for today for every asset matched by ISIN.
    /// Safe to call on every analysis request; exits almost immediately when already done today.
    /// </summary>
    public async Task EnsureTodaySnapshotsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var allFundIds = _providers.SelectMany(p => p.FundIds).ToList();

        // Load existing meta records for all known fund IDs
        var metas = await db.FundPortfolioMetas
            .Where(m => allFundIds.Contains(m.FundIsin))
            .ToListAsync();

        // Short-circuit: every fund has already been checked today
        if (metas.Count == allFundIds.Count && metas.All(m => m.LastRunDate == today))
            return;

        var holdingsByIsin = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        var anyUpdated = false;

        foreach (var provider in _providers)
        {
            IReadOnlyList<FundHoldingResult> results;
            try { results = await provider.GetAllHoldingsAsync(); }
            catch { continue; }

            foreach (var result in results)
            {
                var meta = metas.FirstOrDefault(m => m.FundIsin == result.FundId);
                if (meta is null)
                {
                    meta = new FundPortfolioMeta { FundIsin = result.FundId };
                    db.FundPortfolioMetas.Add(meta);
                    metas.Add(meta);
                }

                if (result.PortfolioDate != meta.LastPortfolioDate)
                {
                    anyUpdated = true;
                    meta.LastPortfolioDate = result.PortfolioDate;
                }

                meta.LastRunDate = today;

                // Always collect the current holdings so the snapshot reflects the full picture
                foreach (var holding in result.Holdings)
                {
                    if (!holdingsByIsin.TryGetValue(holding.Isin, out var list))
                    {
                        list = [];
                        holdingsByIsin[holding.Isin] = list;
                    }
                    list.Add(holding.Percent);
                }
            }
        }

        if (anyUpdated && holdingsByIsin.Count > 0)
            await WriteSnapshotsAsync(today, holdingsByIsin);

        await db.SaveChangesAsync();
    }

    private async Task WriteSnapshotsAsync(DateTime date, Dictionary<string, List<double>> holdingsByIsin)
    {
        var isins = holdingsByIsin.Keys.ToList();

        var matchedAssets = await db.Assets
            .Where(a => a.Isin != null && isins.Contains(a.Isin))
            .ToListAsync();

        var existingByAsset = await db.FundHoldingSnapshots
            .Where(s => s.Date == date)
            .ToDictionaryAsync(s => s.AssetId);

        foreach (var asset in matchedAssets)
        {
            if (!holdingsByIsin.TryGetValue(asset.Isin!, out var percents))
                continue;

            var mean   = percents.Average();
            var median = Median(percents);
            var count  = percents.Count;

            if (existingByAsset.TryGetValue(asset.Id, out var existing))
            {
                existing.MeanFundPercentage   = mean;
                existing.MedianFundPercentage = median;
                existing.NumFundsRepresented  = count;
            }
            else
            {
                db.FundHoldingSnapshots.Add(new FundHoldingSnapshot
                {
                    AssetId              = asset.Id,
                    Date                 = date,
                    MeanFundPercentage   = mean,
                    MedianFundPercentage = median,
                    NumFundsRepresented  = count,
                });
            }
        }
    }

    /// <summary>
    /// Returns an institutional order-flow delta in [-1, 1] for the given asset,
    /// computed as the normalised change in mean fund percentage between the two most
    /// recent snapshots: clamp((latest - previous) / 100, -1, 1).
    /// Returns 0.0 when fewer than two snapshots exist (no baseline available).
    /// </summary>
    public async Task<double> GetInstitutionalDeltaAsync(Guid assetId)
    {
        var snapshots = await db.FundHoldingSnapshots
            .Where(s => s.AssetId == assetId)
            .OrderByDescending(s => s.Date)
            .Take(2)
            .ToListAsync();

        if (snapshots.Count < 2) return 0.0;

        var delta = (snapshots[0].MeanFundPercentage - snapshots[1].MeanFundPercentage) / 100.0;
        return Math.Clamp(delta, -1.0, 1.0);
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}
