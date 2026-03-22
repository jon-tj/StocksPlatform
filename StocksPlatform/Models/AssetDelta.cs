namespace StocksPlatform.Models;

/// <summary>
/// One complete delta snapshot for a given asset on a given UTC date.
/// Computed daily and retained for one year.
/// PairDelta is nullable — only present when a reference pair asset exists.
/// All other deltas are always populated.
/// </summary>
public class AssetDelta
{
    public int Id { get; set; }

    /// <summary>Foreign key to the Asset table.</summary>
    public Guid AssetId { get; set; }

    /// <summary>Navigation property.</summary>
    public Asset Asset { get; set; } = null!;

    /// <summary>UTC date this snapshot covers (time component always 00:00:00).</summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Market delta — derived from covariance imputation using the rolling
    /// whole-market covariance matrix.
    /// </summary>
    public double MarketDelta { get; set; }

    /// <summary>
    /// Pair delta — relative delta against the most relevant correlated asset.
    /// Null when no suitable pair has been identified.
    /// </summary>
    public double? PairDelta { get; set; }

    /// <summary>AssetId of the pair asset used for PairDelta, if any.</summary>
    public Guid? PairAssetId { get; set; }

    /// <summary>Public sentiment delta — aggregated from public news/social signals.</summary>
    public double PublicSentimentDelta { get; set; }

    /// <summary>Member sentiment delta — aggregated from platform member poll forecasts.</summary>
    public double MemberSentimentDelta { get; set; }

    /// <summary>Fundamental delta — based on earnings, ratios and balance-sheet signals.</summary>
    public double FundamentalDelta { get; set; }

    /// <summary>Institutional order-flow delta — derived from large-lot tape signals.</summary>
    public double InstitutionalOrderFlowDelta { get; set; }

    /// <summary>
    /// Pattern delta — measures how closely the last 30 days of price history resembles
    /// the ideal dip-buying setup: a gradual linear decline of ~0.1%/day for the first
    /// 27 days followed by a total drop of ≥10% by the most recent price.
    /// Range: 1.0 (no pattern) → 2.0 (perfect match). Recent days are weighted more heavily.
    /// </summary>
    public double PatternDelta { get; set; } = 1.0;

    /// <summary>
    /// Bull/bear certificate sentiment delta for Nordnet stocks.
    /// Derived from the weighted ratio of long vs short open interest across all listed
    /// bull/bear certificates for this underlying.
    /// Range: −1.0 (all-short) to +1.0 (all-long), neutral at 0.0.
    /// 0.0 is returned for non-Nordnet assets or when no certificates exist.
    /// </summary>
    public double BnbDelta { get; set; } = 0.0;

    /// <summary>
    /// UTC timestamp after which this snapshot should be recomputed.
    /// Null means it has never been computed (legacy rows).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
