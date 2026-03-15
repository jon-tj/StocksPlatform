namespace StocksPlatform.Services.PriceServices;

/// <summary>
/// Abstraction for a price data provider. Implementations may source data from
/// different APIs (e.g. e24.no for Norwegian/NASDAQ stocks, or a crypto exchange).
/// </summary>
public interface IAssetPriceProvider
{
    /// <summary>Ensures daily OHLC bars are up-to-date in the database.</summary>
    Task EnsureDailyBarsAsync(Guid assetId, string symbol, string exchange);

    /// <summary>Ensures intraday bars are up-to-date in the database.</summary>
    Task EnsureIntradayBarsAsync(Guid assetId, string symbol, string exchange);

    /// <summary>Fetches the current live price for the given asset.</summary>
    Task<decimal> FetchLivePriceAsync(Guid assetId);
}
