using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services.Seeding;

/// <summary>
/// Seeds major FX assets (Yahoo symbols like "EURUSD=X") into the Assets table.
/// Safe to run on every startup (idempotent).
/// </summary>
public static class CurrencyPairSeeder
{
    private static readonly (string Symbol, string Name, string? Country)[] FxPairSeed =
    [
        ("EURUSD=X", "Euro / US Dollar", "Euro Area"),
        ("NOKUSD=X", "Norwegian Krone / US Dollar", "Norway"),
        ("GBPUSD=X", "British Pound / US Dollar", "United Kingdom"),
        ("JPYUSD=X", "Japanese Yen / US Dollar", "Japan"),
        ("CHFUSD=X", "Swiss Franc / US Dollar", "Switzerland"),
        ("AUDUSD=X", "Australian Dollar / US Dollar", "Australia"),
        ("CADUSD=X", "Canadian Dollar / US Dollar", "Canada"),
        ("SEKUSD=X", "Swedish Krona / US Dollar", "Sweden"),
        ("NZDUSD=X", "New Zealand Dollar / US Dollar", "New Zealand"),
        ("CNYUSD=X", "Chinese Yuan / US Dollar", "China"),
        ("INRUSD=X", "Indian Rupee / US Dollar", "India"),
        ("BRLUSD=X", "Brazilian Real / US Dollar", "Brazil"),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        var symbols = FxPairSeed
            .Select(s => s.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existing = await db.Assets
            .Where(a => a.Symbol != null && symbols.Contains(a.Symbol))
            .ToListAsync();

        var bySymbol = existing
            .Where(a => a.Symbol != null)
            .ToDictionary(a => a.Symbol!, StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<Asset>();
        foreach (var (symbol, name, country) in FxPairSeed)
        {
            if (bySymbol.TryGetValue(symbol, out var asset))
            {
                if (string.IsNullOrWhiteSpace(asset.Name)) asset.Name = name;
                if (string.IsNullOrWhiteSpace(asset.Country) && !string.IsNullOrWhiteSpace(country)) asset.Country = country;
                if (string.IsNullOrWhiteSpace(asset.Region)) asset.Region = "Global";
                if (string.IsNullOrWhiteSpace(asset.Sector)) asset.Sector = "Foreign Exchange";
                if (string.IsNullOrWhiteSpace(asset.Subsector)) asset.Subsector = "Major Currency Pairs";
                if (string.IsNullOrWhiteSpace(asset.Broker)) asset.Broker = "Yahoo";
                if (asset.Type != AssetType.Currency) asset.Type = AssetType.Currency;
                continue;
            }

            toInsert.Add(new Asset
            {
                Id = AppDbContext.AssetGuid($"fx-{symbol.ToLowerInvariant()}"),
                Name = name,
                Type = AssetType.Currency,
                Symbol = symbol,
                Market = null,
                Broker = "Yahoo",
                BrokerSymbol = symbol,
                Country = country,
                Region = "Global",
                Sector = "Foreign Exchange",
                Subsector = "Major Currency Pairs",
            });
        }

        if (toInsert.Count > 0)
            db.Assets.AddRange(toInsert);

        await db.SaveChangesAsync();
    }
}
