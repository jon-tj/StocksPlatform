using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Models;

namespace StocksPlatform.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<UserPortfolio> UserPortfolios => Set<UserPortfolio>();
    public DbSet<Poll> Polls => Set<Poll>();
    public DbSet<PollQuestion> PollQuestions => Set<PollQuestion>();
    public DbSet<PollResponse> PollResponses => Set<PollResponse>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Asset>()
            .Property(a => a.Id)
            .ValueGeneratedNever();

        // Seed the default main portfolio asset
        builder.Entity<Asset>().HasData(new Asset
        {
            Id = Guid.Empty,
            Name = "Main Portfolio",
            Type = AssetType.Portfolio,
        });
    }
}

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
