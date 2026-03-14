namespace StocksPlatform.Models;

/// <summary>
/// Latest known price for an asset, cached in the database.
/// </summary>
public class AssetPrice
{
    public int Id { get; set; }

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    /// <summary>Price in NOK.</summary>
    public decimal Price { get; set; }

    /// <summary>UTC timestamp when this price was fetched.</summary>
    public DateTime FetchedAt { get; set; }

    /// <summary>UTC timestamp after which this price should be refreshed.</summary>
    public DateTime ExpiresAt { get; set; }
}

public class AssetIntradayHistory
{
    public int Id { get; set; }
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public long? Volume { get; set; }
}
public class AssetDailyHistory
{
    public int Id { get; set; }
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public long? Volume { get; set; }
    public decimal? Dividend { get; set; } = null;
}

/// <summary>
/// Tracks the last time daily price history was fetched from e24.no for an asset.
/// Used to decide whether to fetch period=10years (first/stale) or period=1months (recent).
/// </summary>
public class AssetPriceMeta
{
    public int Id { get; set; }

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    /// <summary>UTC timestamp of the last successful daily fetch. Null if never fetched.</summary>
    public DateTime? LastDailyFetchAt { get; set; }
}