namespace StocksPlatform.Models;

/// <summary>
/// Latest known price for an asset, cached in the database.
/// </summary>
public class AssetPrice
{
    public int Id { get; set; }

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    /// <summary>Price in USD (or base currency).</summary>
    public decimal Price { get; set; }

    /// <summary>UTC timestamp when this price was fetched.</summary>
    public DateTime FetchedAt { get; set; }

    /// <summary>UTC timestamp after which this price should be refreshed.</summary>
    public DateTime ExpiresAt { get; set; }
}
