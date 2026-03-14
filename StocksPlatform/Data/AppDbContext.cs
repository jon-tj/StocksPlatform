using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Models;

namespace StocksPlatform.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<PortfolioAsset> PortfolioAssets => Set<PortfolioAsset>();
    public DbSet<UserPortfolio> UserPortfolios => Set<UserPortfolio>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollQuestion> PollQuestions => Set<PollQuestion>();
    public DbSet<PollResponse> PollResponses => Set<PollResponse>();
    public DbSet<AssetDelta> AssetDeltas => Set<AssetDelta>();
    public DbSet<AssetPrice> AssetPrices => Set<AssetPrice>();
    public DbSet<AssetDailyHistory> AssetDailyHistory => Set<AssetDailyHistory>();
    public DbSet<AssetIntradayHistory> AssetIntradayHistory => Set<AssetIntradayHistory>();
    public DbSet<AssetPriceMeta> AssetHistoryMeta => Set<AssetPriceMeta>();

    // Stable, deterministic asset IDs derived from a fixed namespace + name.
    // Equivalent to UUID v5 (SHA-1 name-based) — same input always yields same GUID.
    public static readonly Guid PortfolioId = Guid.Empty;
    public static readonly Guid AaplId      = AssetGuid("AAPL");
    public static readonly Guid NvdaId      = AssetGuid("NVDA");
    public static readonly Guid MsftId      = AssetGuid("MSFT");
    public static readonly Guid MetaId      = AssetGuid("META");
    public static readonly Guid AmznId      = AssetGuid("AMZN");

    /// <summary>
    /// Produces a deterministic GUID from a name string using SHA-1,
    /// formatted as a Version-5 UUID (RFC 4122).
    /// </summary>
    public static Guid AssetGuid(string name)
    {
        // Fixed namespace: 6ba7b810-9dad-11d1-80b4-00c04fd430c8 (URL namespace)
        Span<byte> ns = stackalloc byte[]
        {
            0x6b, 0xa7, 0xb8, 0x10, 0x9d, 0xad, 0x11, 0xd1,
            0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8,
        };
        var nameBytes = Encoding.UTF8.GetBytes(name);
        Span<byte> input = stackalloc byte[ns.Length + nameBytes.Length];
        ns.CopyTo(input);
        nameBytes.CopyTo(input[ns.Length..]);

        var hash = SHA1.HashData(input);

        // Set version (5) and variant bits per RFC 4122
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash[..16]);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Asset>()
            .Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Entity<AssetDelta>()
            .HasIndex(d => new { d.AssetId, d.Date })
            .IsUnique();

        builder.Entity<AssetPrice>()
            .HasIndex(p => p.AssetId)
            .IsUnique();

        builder.Entity<AssetDailyHistory>()
            .HasIndex(b => new { b.AssetId, b.Timestamp })
            .IsUnique();

        builder.Entity<AssetIntradayHistory>()
            .HasIndex(b => new { b.AssetId, b.Timestamp })
            .IsUnique();

        builder.Entity<AssetPriceMeta>()
            .HasIndex(m => m.AssetId)
            .IsUnique();

        // PortfolioAsset: unique pair, parent must navigate as Portfolio
        builder.Entity<PortfolioAsset>()
            .HasIndex(pa => new { pa.PortfolioId, pa.AssetId })
            .IsUnique();

        builder.Entity<PortfolioAsset>()
            .HasOne(pa => pa.Portfolio)
            .WithMany()
            .HasForeignKey(pa => pa.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PortfolioAsset>()
            .HasOne(pa => pa.Asset)
            .WithMany()
            .HasForeignKey(pa => pa.AssetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Seed assets
        builder.Entity<Asset>().HasData(
            new Asset { Id = PortfolioId, Name = "Main Portfolio", Type = AssetType.Portfolio },
            new Asset { Id = AaplId, Name = "Apple Inc.",      Type = AssetType.Stock, Symbol = "AAPL", Market = "NASDAQ", Broker = "NordNet", BrokerSymbol = "AAPL" },
            new Asset { Id = NvdaId, Name = "NVIDIA Corp.",    Type = AssetType.Stock, Symbol = "NVDA", Market = "NASDAQ", Broker = "NordNet", BrokerSymbol = "NVDA" },
            new Asset { Id = MsftId, Name = "Microsoft Corp.", Type = AssetType.Stock, Symbol = "MSFT", Market = "NASDAQ", Broker = "NordNet", BrokerSymbol = "MSFT" },
            new Asset { Id = MetaId, Name = "Meta Platforms",  Type = AssetType.Stock, Symbol = "META", Market = "NASDAQ", Broker = "NordNet", BrokerSymbol = "META" },
            new Asset { Id = AmznId, Name = "Amazon.com Inc.", Type = AssetType.Stock, Symbol = "AMZN", Market = "NASDAQ", Broker = "NordNet", BrokerSymbol = "AMZN" }
        );

        // Seed portfolio membership
        builder.Entity<PortfolioAsset>().HasData(
            new PortfolioAsset { Id = 1, PortfolioId = PortfolioId, AssetId = AaplId, Quantity = 12 },
            new PortfolioAsset { Id = 2, PortfolioId = PortfolioId, AssetId = NvdaId, Quantity =  5 },
            new PortfolioAsset { Id = 3, PortfolioId = PortfolioId, AssetId = MsftId, Quantity =  8 },
            new PortfolioAsset { Id = 4, PortfolioId = PortfolioId, AssetId = MetaId, Quantity =  3 },
            new PortfolioAsset { Id = 5, PortfolioId = PortfolioId, AssetId = AmznId, Quantity =  6 }
        );
    }
}

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
