namespace StocksPlatform.Models;

public enum AssetType
{
    Portfolio = 0,
    Stock = 1,
    Commodity = 2,
    Crypto = 3,
}

public class Asset
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AssetType Type { get; set; }
}

public class UserPortfolio
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
}
