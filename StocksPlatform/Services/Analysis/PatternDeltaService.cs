using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;

namespace StocksPlatform.Services.Analysis;

public class PatternDeltaService(AppDbContext db, OnnxPriceModelRegistry modelRegistry) : IAssetDeltaProvider
{
    private const int WindowDays = 30;
    private const int DropDays = 3;
    private const double IdealDailyDecline = -0.001;      // -0.1 % per day
    private const double IdealTotalLogDrop = -0.10536;    // ln(0.90) ≈ -10 %
    private const double WeightAlpha = 3.0;               // exponential weight steepness
    private const double RmseToleranceSq = 0.05 * 0.05;  // 5 % RMSE → half score

    public async Task<double> ComputeAsync(Guid assetId, DateTime date)
    {
        var dailyPrices = await db.AssetDailyHistory
            .Where(h => h.AssetId == assetId)
            .OrderBy(h => h.Timestamp)
            .Select(h => (double)h.Price)
            .Take(WindowDays + 1)
            .ToListAsync();

        if (dailyPrices.Count < 5) return 0.0;

        // --- ML inference path ---
        if (modelRegistry.HasModels && dailyPrices.Count >= OnnxPriceModelRegistry.WindowSize + 1)
        {
            var symbol = await db.Assets
                .Where(a => a.Id == assetId)
                .Select(a => a.Symbol)
                .FirstOrDefaultAsync();

            if (symbol is not null)
            {
                var logReturns = BuildLogReturns(dailyPrices);

                // 1. Try models for this exact symbol
                var upProb = modelRegistry.RunInference(symbol, logReturns);

                // 2. Fall back to the closest correlated symbol that has models
                if (upProb is null)
                {
                    var closest = modelRegistry.FindClosestSymbolWithModels(symbol);
                    if (closest is not null)
                        upProb = modelRegistry.RunInference(closest.Value.Symbol, logReturns);
                }

                if (upProb is not null)
                    return (double)upProb.Value * 2 - 1; // scale [0,1] → [-1,1]
            }
        }

        return 0.0;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Converts a price series into the last <see cref="OnnxPriceModelRegistry.WindowSize"/>
    /// log-return values (float32) expected by the ONNX models.
    /// </summary>
    private static float[] BuildLogReturns(List<double> prices)
    {
        int n = OnnxPriceModelRegistry.WindowSize;
        // Take the last (n+1) prices so we get exactly n log-returns
        int start = Math.Max(0, prices.Count - n - 1);
        var window = prices.Skip(start).ToList();

        var returns = new float[Math.Min(n, window.Count - 1)];
        for (int i = 0; i < returns.Length; i++)
            returns[i] = (float)Math.Log(window[i + 1] / window[i]);

        // If shorter than n (shouldn't happen given the guard above), pad with zeros
        if (returns.Length < n)
        {
            var padded = new float[n];
            Array.Copy(returns, 0, padded, n - returns.Length, returns.Length);
            return padded;
        }

        return returns;
    }
}
