using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using StocksPlatform.Data;
using StocksPlatform.Models;

namespace StocksPlatform.Services.Analysis;

/// <summary>
/// Computes <c>BnbDelta</c> for a stock by querying Nordnet's bull/bear certificate list
/// for that underlying, weighing long vs short open interest.
/// Certificate data is persisted as <see cref="BullBearCertificateSnapshot"/> rows so that
/// subsequent calls within the same UTC day skip the network request entirely.
/// </summary>
/// <remarks>
/// Algorithm (mirrors the FetchBullBear research notebook):
/// <list type="number">
///   <item>If today's rows already exist in the DB, read from cache (no HTTP call).</item>
///   <item>Otherwise fetch from Nordnet (<c>underlying_name = {asset.Name.ToUpper()}</c>),
///         persist each certificate row, and prune stale rows older than 30 days.</item>
///   <item>For each certificate compute <c>weight = holders^0.7 × gearing</c>.</item>
///   <item><c>ratio_long = Σ(long weights) / Σ(all weights)</c></item>
///   <item><c>BnbDelta = ratio_long × 2 − 1</c>  → range [−1, 1], neutral at 0.0</item>
/// </list>
/// Returns 0.0 (neutral) when the asset is not a Nordnet stock, when no certificates are
/// found, or on any network/parse error.
/// </remarks>
public class BullBearDeltaService(
    HttpClient httpClient,
    AppDbContext db,
    ILogger<BullBearDeltaService> logger)
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    private const string BullBearListUrl =
        "https://www.nordnet.no/api/2/instrument_search/query/bullbearlist" +
        "?apply_filters=underlying_name%3D{0}%7Cnordnet_markets%3Dtrue&limit=200";

    /// <summary>
    /// Returns the BnbDelta for today.  Uses cached DB rows when available.
    /// Caller is responsible for <c>SaveChangesAsync</c> (same pattern as the other delta services).
    /// </summary>
    public async Task<double> ComputeAsync(Guid assetId)
    {
        var asset = await db.Assets.FindAsync(assetId);
        if (asset is null) return 0.0;

        // Only applicable to Nordnet-brokered stocks.
        if (asset.Broker?.Contains("Nordnet", StringComparison.OrdinalIgnoreCase) != true)
            return 0.0;

        if (string.IsNullOrWhiteSpace(asset.Name))
            return 0.0;

        var today = DateTime.UtcNow.Date;

        // ── Check cache ───────────────────────────────────────────────────────
        var cached = await db.BullBearCertificateSnapshots
            .Where(s => s.AssetId == assetId && s.Date == today)
            .ToListAsync();

        if (cached.Count > 0)
            return DeltaFromRows(cached);

        // ── Fetch from Nordnet ────────────────────────────────────────────────
        try
        {
            var underlyingName = Uri.EscapeDataString(asset.Name.ToUpperInvariant());
            var url = string.Format(BullBearListUrl, underlyingName);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("accept", "application/json");
            request.Headers.Add("ntag", "NO_NTAG_RECEIVED_YET");
            request.Headers.Add("client-id", "NEXT");

            using var response = await httpClient.SendAsync(request);
            logger.LogInformation("Fetched bull/bear certificate list for {AssetId} ({Name}): {StatusCode}",
                assetId, asset.Name, response.StatusCode);
            if (!response.IsSuccessStatusCode)
                return 0.0;

            var data = await response.Content.ReadFromJsonAsync<BullBearResponse>();
            logger.LogInformation("Parsed bull/bear certificate response for {AssetId} ({Name}): {CertificateCount} certificates found",
                assetId, asset.Name, data?.Results?.Count ?? 0);
            if (data?.Results is not { Count: > 0 })
                return 0.0;

            var rows = data.Results
                .Where(r =>
                    r.StatisticalInfo?.NumberOfOwners is not null &&
                    r.CertificateInfo?.StaticLeverage is not null &&
                    r.EtpInfo?.Direction is not null)
                .Select(r =>
                {
                    var holders = r.StatisticalInfo!.NumberOfOwners!.Value;
                    var gearing = r.CertificateInfo!.StaticLeverage!.Value;
                    return new BullBearCertificateSnapshot
                    {
                        AssetId   = assetId,
                        Date      = today,
                        Direction = r.EtpInfo!.Direction!,
                        Gearing   = gearing,
                        Holders   = (int)holders,
                        Weight    = Math.Pow(holders, 0.7) * gearing,
                    };
                })
                .ToList();

            if (rows.Count == 0)
                return 0.0;

            // Persist and prune stale rows (caller saves changes).
            await db.BullBearCertificateSnapshots
                .Where(s => s.AssetId == assetId && s.Date < today.Subtract(StaleAfter))
                .ExecuteDeleteAsync();

            db.BullBearCertificateSnapshots.AddRange(rows);

            return DeltaFromRows(rows);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to compute BnbDelta for {AssetId} ({Name})", assetId, asset.Name);
            return 0.0;
        }
    }

    /// <summary>
    /// Returns the most recent persisted certificate rows for an asset (latest date available).
    /// Used by the controller to serve the drilldown view without recomputing.
    /// </summary>
    public async Task<List<BullBearCertificateSnapshot>> GetLatestSnapshotsAsync(Guid assetId)
    {
        var latest = await db.BullBearCertificateSnapshots
            .Where(s => s.AssetId == assetId)
            .OrderByDescending(s => s.Date)
            .Select(s => s.Date)
            .FirstOrDefaultAsync();

        if (latest == default)
            return [];

        return await db.BullBearCertificateSnapshots
            .Where(s => s.AssetId == assetId && s.Date == latest)
            .OrderByDescending(s => s.Weight)
            .ToListAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double DeltaFromRows(List<BullBearCertificateSnapshot> rows)
    {
        double longWeight  = rows.Where(r => r.Direction.Equals("Long",  StringComparison.OrdinalIgnoreCase)).Sum(r => r.Weight);
        double totalWeight = rows.Sum(r => r.Weight);

        if (totalWeight <= 0)
            return 0.0;

        return longWeight / totalWeight * 2.0 - 1.0;
    }

    // ── JSON deserialization shapes ───────────────────────────────────────────

    private class BullBearResponse
    {
        [JsonPropertyName("results")]
        public List<BullBearResult>? Results { get; set; }
    }

    private class BullBearResult
    {
        [JsonPropertyName("statistical_info")]
        public StatisticalInfoDto? StatisticalInfo { get; set; }

        [JsonPropertyName("certificate_info")]
        public CertificateInfoDto? CertificateInfo { get; set; }

        [JsonPropertyName("etp_info")]
        public EtpInfoDto? EtpInfo { get; set; }
    }

    private class StatisticalInfoDto
    {
        [JsonPropertyName("number_of_owners")]
        public double? NumberOfOwners { get; set; }
    }

    private class CertificateInfoDto
    {
        [JsonPropertyName("static_leverage")]
        public double? StaticLeverage { get; set; }
    }

    private class EtpInfoDto
    {
        [JsonPropertyName("direction")]
        public string? Direction { get; set; }
    }
}
