namespace StocksPlatform.Models;

public enum OrderBookSide
{
    Bid = 0,
    Ask = 1,
}

/// <summary>
/// Records a change in volume at a specific order book level for one side (bid or ask).
/// Only written when the volume at that level differs from the previously recorded value.
/// </summary>
public class OrderBookSnapshot
{
    public int Id { get; set; }

    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    /// <summary>Depth level (1 = best bid/ask, 2 = next, etc.)</summary>
    public int Level { get; set; }

    public OrderBookSide Side { get; set; }

    /// <summary>Price at this level at the time of the snapshot.</summary>
    public double Price { get; set; }

    /// <summary>Total volume at this level after the change.</summary>
    public double NewVol { get; set; }

    /// <summary>Change in volume compared to the previous recorded value (positive = added, negative = removed).</summary>
    public double Increment { get; set; }
}
