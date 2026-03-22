using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services;

/// <summary>
/// Background service that polls the E24 Infront order book every 10 minutes for all
/// Norwegian (Country = "NO") stocks with XOSL or XNAS market listing.
///
/// On each poll, per-level bid and ask volumes are compared against the last recorded
/// value. A new <see cref="OrderBookSnapshot"/> row is persisted only when a volume
/// changes, recording the new volume and the increment (positive = depth added,
/// negative = depth removed).
/// </summary>
public class OrderBookPollingService(
    IServiceScopeFactory scopeFactory,
    ILogger<OrderBookPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    // In-memory last-known volume state: (assetId, level, side) → volume
    private readonly Dictionary<(Guid, int, OrderBookSide), double> _lastVol = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Seed in-memory state from the database so increments are correct after a restart
        await InitializeLastVolAsync(stoppingToken);

        // Stagger first run so startup isn't flooded with HTTP calls
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollAllAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task InitializeLastVolAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var latest = await db.OrderBookSnapshots
            .GroupBy(o => new { o.AssetId, o.Level, o.Side })
            .Select(g => new
            {
                g.Key.AssetId,
                g.Key.Level,
                g.Key.Side,
                NewVol = g.OrderByDescending(o => o.Timestamp).First().NewVol,
            })
            .ToListAsync(ct);

        foreach (var s in latest)
            _lastVol[(s.AssetId, s.Level, s.Side)] = s.NewVol;

        logger.LogInformation("OrderBook: seeded _lastVol with {Count} entries from database", latest.Count);
    }

    private async Task PollAllAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orderBookSvc = scope.ServiceProvider.GetRequiredService<OrderBookService>();

        var noAssets = await db.Assets
            .Where(a => a.Country == "Norway" && (a.Market == "XOSL" || a.Market == "XNAS") && a.Symbol != null)
            .Select(a => new { a.Id, a.Symbol, a.Market })
            .ToListAsync(ct);

        if (noAssets.Count == 0) return;

        logger.LogInformation("OrderBook poll: fetching {Count} Norwegian assets", noAssets.Count);

        var shuffled = noAssets.OrderBy(_ => Random.Shared.Next()).ToList();
        var now = DateTime.UtcNow;
        var newSnapshots = new List<OrderBookSnapshot>();

        for (int i = 0; i < shuffled.Count; i++)
        {
            if (ct.IsCancellationRequested) break;
            if (i >= 10 && newSnapshots.Count == 0)
            {
                logger.LogInformation("OrderBook poll: no changes in first 10 probes, market likely closed — skipping remaining {Count} assets", shuffled.Count - i);
                break;
            }

            var asset = shuffled[i];
            try
            {
                var exchange = asset.Market == "XOSL" ? "OSE" : "NAS";
                var levels = await orderBookSvc.GetOrderBookAsync(asset.Symbol!, exchange, limit: 20);

                for (int j = 0; j < levels.Count; j++)
                {
                    TryRecordChange(newSnapshots, asset.Id, now, j + 1,
                        OrderBookSide.Bid, levels[j].Bid, levels[j].BidVol);
                    TryRecordChange(newSnapshots, asset.Id, now, j + 1,
                        OrderBookSide.Ask, levels[j].Ask, levels[j].AskVol);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OrderBook poll failed for {AssetId}", asset.Id);
            }
        }

        if (newSnapshots.Count > 0)
        {
            db.OrderBookSnapshots.AddRange(newSnapshots);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("OrderBook poll: persisted {Count} change records", newSnapshots.Count);
        }
    }

    private void TryRecordChange(
        List<OrderBookSnapshot> batch,
        Guid assetId, DateTime timestamp, int level,
        OrderBookSide side, double price, double newVol)
    {
        var key = (assetId, level, side);
        if (_lastVol.TryGetValue(key, out var prev) && Math.Abs(prev - newVol) < 0.0001)
            return; // no change

        var increment = _lastVol.TryGetValue(key, out var old) ? newVol - old : newVol;
        _lastVol[key] = newVol;

        batch.Add(new OrderBookSnapshot
        {
            AssetId   = assetId,
            Timestamp = timestamp,
            Level     = level,
            Side      = side,
            Price     = price,
            NewVol    = newVol,
            Increment = increment,
        });
    }
}
