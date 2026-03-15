namespace StocksPlatform.Models;

/// <summary>
/// Daily snapshot of how a given asset is represented across all tracked funds.
/// One row per (AssetId, Date). Values are recomputed whenever at least one fund
/// publishes a new portfolio update.
/// </summary>
public class FundHoldingSnapshot
{
    public int Id { get; set; }

    /// <summary>Foreign key to the Asset table.</summary>
    public Guid AssetId { get; set; }

    /// <summary>Navigation property.</summary>
    public Asset Asset { get; set; } = null!;

    /// <summary>UTC date this snapshot covers (time component always 00:00:00).</summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Mean of the asset's percentage weight across all funds that hold it.
    /// Represents the average allocation (0–100).
    /// </summary>
    public double MeanFundPercentage { get; set; }

    /// <summary>
    /// Median of the asset's percentage weight across all funds that hold it.
    /// </summary>
    public double MedianFundPercentage { get; set; }

    /// <summary>Number of tracked funds that held this asset at the time of the snapshot.</summary>
    public int NumFundsRepresented { get; set; }
}

/// <summary>
/// Tracks the last portfolio-date and last run-date seen for each tracked fund ISIN.
/// Used to detect when fund holdings have actually changed so we can skip redundant refreshes.
/// </summary>
public class FundPortfolioMeta
{
    public int Id { get; set; }

    /// <summary>ISIN of the fund being tracked.</summary>
    public string FundIsin { get; set; } = string.Empty;

    /// <summary>
    /// The "lastUpdated.portfolio" value returned by the API on the last successful call.
    /// Stored as a string (e.g. "2026-02-28") to match the API format exactly.
    /// </summary>
    public string? LastPortfolioDate { get; set; }

    /// <summary>UTC date on which we last ran the refresh for this fund.</summary>
    public DateTime? LastRunDate { get; set; }
}
