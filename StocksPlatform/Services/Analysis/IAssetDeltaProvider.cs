namespace StocksPlatform.Services.Analysis;

/// <summary>
/// Computes a single normalised delta score (typically 0–2, neutral at 1.0)
/// for a given asset on a given date. Implementations cover distinct signal
/// categories such as price pattern, public sentiment, or fundamental analysis.
/// </summary>
public interface IAssetDeltaProvider
{
    Task<double> ComputeAsync(Guid assetId, DateTime date);
}
