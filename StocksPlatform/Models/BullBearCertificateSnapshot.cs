namespace StocksPlatform.Models;

/// <summary>
/// Persisted snapshot of a single Nordnet bull/bear certificate for a given underlying asset on a given date.
/// One row per certificate per (AssetId, Date). Stale rows (older than today) are pruned by the seeding logic.
/// </summary>
public class BullBearCertificateSnapshot
{
    public int Id { get; set; }

    /// <summary>Foreign key to the Asset table (the underlying stock, not the certificate itself).</summary>
    public Guid AssetId { get; set; }

    /// <summary>Navigation property.</summary>
    public Asset Asset { get; set; } = null!;

    /// <summary>UTC date this snapshot was fetched (time component always 00:00:00).</summary>
    public DateTime Date { get; set; }

    /// <summary>"Long" (bull) or "Short" (bear).</summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>Static leverage / gearing of the certificate (e.g. 5.0 for 5x).</summary>
    public double Gearing { get; set; }

    /// <summary>Number of Nordnet account holders who own this certificate.</summary>
    public int Holders { get; set; }

    /// <summary>
    /// Pre-computed weight used in the BnbDelta formula: <c>Holders^0.7 × Gearing</c>.
    /// Stored so the delta can be recomputed from cache without exponentiating again.
    /// </summary>
    public double Weight { get; set; }
}
