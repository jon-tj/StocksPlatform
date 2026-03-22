namespace StocksPlatform.Models;

/// <summary>
/// Persisted result of a fundamental valuation computation for a given asset and date.
/// Stores raw financial inputs, intermediate calculations, and the final
/// FundamentalDelta, along with peer-P/E context for verification and display.
/// </summary>
public class FundamentalSnapshot
{
    public int Id { get; set; }

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    /// <summary>UTC date this snapshot covers (time component always 00:00:00).</summary>
    public DateTime Date { get; set; }

    // ── Raw financial data from Yahoo Finance ─────────────────────────────────

    /// <summary>Trailing twelve-month diluted EPS (reporting currency).</summary>
    public double? TrailingEps { get; set; }

    /// <summary>Normalized (ex-items) trailing diluted EPS.</summary>
    public double? NormalizedEps { get; set; }

    /// <summary>Trailing twelve-month total revenue.</summary>
    public double? TrailingRevenue { get; set; }

    /// <summary>Trailing twelve-month EBITDA.</summary>
    public double? TrailingEbitda { get; set; }

    /// <summary>Trailing twelve-month operating income.</summary>
    public double? TrailingOperatingIncome { get; set; }

    /// <summary>Trailing twelve-month net income.</summary>
    public double? TrailingNetIncome { get; set; }

    /// <summary>Trailing twelve-month dividend per share.</summary>
    public double? TrailingDividendPerShare { get; set; }

    // ── Derived inputs ────────────────────────────────────────────────────────

    /// <summary>Estimated annual revenue CAGR derived from multi-year Yahoo data.</summary>
    public double? RevenueGrowthRate { get; set; }

    /// <summary>Current (or most-recently cached) price used for the upside calculation.</summary>
    public double? CurrentPrice { get; set; }

    // ── Valuation estimates ───────────────────────────────────────────────────

    /// <summary>Benjamin Graham intrinsic value: EPS × (8.5 + 2g) × (4.4 / Y).</summary>
    public double? GrahamValue { get; set; }

    /// <summary>Two-stage discounted cash-flow value per share.</summary>
    public double? DcfValue { get; set; }

    /// <summary>Earnings-multiple estimate: EPS × peer-mean P/E.</summary>
    public double? EarningsMultipleValue { get; set; }

    /// <summary>Simple average of available valuation estimates.</summary>
    public double? ConsensusValue { get; set; }

    // ── Peer P/E context ──────────────────────────────────────────────────────

    /// <summary>Mean trailing P/E of the five most-correlated peer stocks.</summary>
    public double? PeerMeanPe { get; set; }

    /// <summary>JSON array of peer ticker symbols whose P/E was used, e.g. ["AAPL","MSFT"].</summary>
    public string? PeerSymbols { get; set; }

    /// <summary>Trailing P/E of this asset itself (currentPrice / trailingEps).</summary>
    public double? AssetCurrentPe { get; set; }

    // ── Result ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fundamental delta in range [−1, 1]: positive = undervalued, negative = overvalued.
    /// Computed as clamp(upside × 2, −1, 1) where upside = consensusValue / currentPrice − 1.
    /// Returns 0.0 when insufficient data is available.
    /// </summary>
    public double FundamentalDelta { get; set; }
}
