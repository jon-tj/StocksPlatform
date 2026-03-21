using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;

namespace StocksPlatform.Services.Analysis;

public class PatternDeltaService(AppDbContext db) : IAssetDeltaProvider
{
    private const int WindowDays = 30;
    private const int DropDays = 3;
    private const double IdealDailyDecline = -0.001;      // -0.1 % per day
    private const double IdealTotalLogDrop = -0.10536;    // ln(0.90) ≈ -10 %
    private const double WeightAlpha = 3.0;               // exponential weight steepness
    private const double RmseToleranceSq = 0.05 * 0.05;  // 5 % RMSE → half score

    public async Task<double> ComputeAsync(Guid assetId, DateTime date)
    {
        var since = date.Date.AddDays(-WindowDays);

        var dailyPrices = await db.AssetDailyHistory
            .Where(h => h.AssetId == assetId
                     && h.Timestamp >= since)
            .OrderBy(h => h.Timestamp)
            .Select(h => (double)h.Price)
            .ToListAsync();

        if (dailyPrices.Count < 5) return 0.0;

        bool isIncreasing = dailyPrices.Last() > dailyPrices.First();
        double wSum = 0, weightedSqErr = 0;

        if (isIncreasing)
        {
            for (int i = 1; i < dailyPrices.Count; i++)
            {
                double t = (double)i / Math.Max(dailyPrices.Count - 1, 1); // 0 → 1
                double w = Math.Exp(WeightAlpha * t); // heavier weight toward recent increases
                wSum += w;
                double uniformDecline = (dailyPrices.Last() - dailyPrices.First()) * (1 - t);
                double err = dailyPrices[i] - dailyPrices[i - 1] - uniformDecline;
                weightedSqErr += w * err * err;
            }
        }
        else
        {
            for (int i = 1; i < dailyPrices.Count - 1; i++)
            {
                double t = (double)i / Math.Max(dailyPrices.Count - 1, 1); // 0 → 1
                double w = Math.Exp(WeightAlpha * t); // heavier weight toward recent declines
                wSum += w;
                double uniformDecrease = -dailyPrices.First() * 0.001;
                double err = dailyPrices[i] - dailyPrices[i - 1] - uniformDecrease;
                weightedSqErr += w * err * err;
            }
            double lastDecline = dailyPrices.Last() - dailyPrices[^2];
            double optimalDecline = dailyPrices.Last() * 0.1; // ideal 10 % drop on the last day
            double wLast = Math.Exp(WeightAlpha); // weight for the last decline
            weightedSqErr += Math.Pow(lastDecline - optimalDecline, 2) * wLast;
            wSum += wLast;
        }
        double priceProduct = wSum * dailyPrices.Last() * dailyPrices.First();
        double wmse = priceProduct != 0 ? weightedSqErr / priceProduct : 1.0;
        return 1.0 - Math.Min(1.0, Math.Sqrt(wmse) / 0.1); // score between 0 and 1, with 10 % RMSE as tolerance
    }
}
