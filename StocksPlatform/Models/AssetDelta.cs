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
    /// UTC timestamp after which this snapshot should be recomputed.
    /// Null means it has never been computed (legacy rows).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
}
