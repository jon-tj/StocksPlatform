using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text.Json;

namespace StocksPlatform.Services.Analysis;

/// <summary>
/// Singleton registry that loads ONNX price-prediction models and a symbol
/// correlation matrix from disk into memory at startup.
///
/// Models are expected at:
///   Services/Analysis/Models/Price/model_{symbol}_{i}.onnx
///
/// Correlation matrix is expected at:
///   Services/Analysis/Models/correlation.json
///   Format: { "AAPL": { "MSFT": 0.82, ... }, ... }
/// </summary>
public sealed class OnnxPriceModelRegistry : IDisposable
{
    public const int WindowSize = 30;

    private readonly Dictionary<string, InferenceSession[]> _models =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Dictionary<string, float>> _correlation =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<OnnxPriceModelRegistry> _logger;

    public OnnxPriceModelRegistry(IWebHostEnvironment env, ILogger<OnnxPriceModelRegistry> logger)
    {
        _logger = logger;
        var basePath = Path.Combine(env.ContentRootPath, "Services", "Analysis", "Models");
        LoadModels(basePath);
        LoadCorrelation(basePath);
        _logger.LogInformation(
            "OnnxPriceModelRegistry: loaded {SymbolCount} symbol(s), correlation matrix has {CorrCount} entries.",
            _models.Count, _correlation.Count);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Whether any ONNX models were found on disk.</summary>
    public bool HasModels => _models.Count > 0;

    /// <summary>
    /// Returns the loaded inference sessions for the symbol, or <c>null</c> if
    /// no model files were found for it.
    /// </summary>
    public InferenceSession[]? GetModelsForSymbol(string symbol) =>
        _models.TryGetValue(symbol, out var sessions) ? sessions : null;

    /// <summary>
    /// Finds the symbol with the highest absolute correlation to <paramref name="symbol"/>
    /// that also has models loaded, returning both the symbol and its correlation value.
    /// Returns <c>null</c> when the correlation matrix is unavailable or no correlated
    /// symbol has models.
    /// </summary>
    public (string Symbol, float Correlation)? FindClosestSymbolWithModels(string symbol)
    {
        if (!_correlation.TryGetValue(symbol, out var row) || _models.Count == 0)
            return null;

        string? best = null;
        float bestCorr = float.MinValue;

        foreach (var (other, corr) in row)
        {
            if (string.Equals(other, symbol, StringComparison.OrdinalIgnoreCase)) continue;
            if (!_models.ContainsKey(other)) continue;
            if (corr > bestCorr)
            {
                bestCorr = corr;
                best = other;
            }
        }

        return best is null ? null : (best, bestCorr);
    }

    /// <summary>
    /// Runs all models for <paramref name="symbol"/> against the supplied log-return
    /// window and returns the average probability of an upward move (index 1 of softmax output).
    /// Returns <c>null</c> if the symbol has no loaded models.
    /// </summary>
    public float? RunInference(string symbol, float[] logReturns)
    {
        var sessions = GetModelsForSymbol(symbol);
        if (sessions is null || sessions.Length == 0) return null;

        float total = 0f;
        foreach (var session in sessions)
            total += InferUpProbability(session, logReturns);

        return total / sessions.Length;
    }

    // -------------------------------------------------------------------------
    // Inference helper
    // -------------------------------------------------------------------------

    private static float InferUpProbability(InferenceSession session, float[] logReturns)
    {
        var inputName = session.InputNames[0];
        var tensor = new DenseTensor<float>(logReturns, new[] { 1, logReturns.Length });
        var inputs = new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        using var results = session.Run(inputs);
        var output = results[0].AsEnumerable<float>().ToArray();
        // output shape: [1, 2] → [down_prob, up_prob]
        return output.Length >= 2 ? output[1] / (output[0] + output[1]) : 0.5f;
    }

    // -------------------------------------------------------------------------
    // Loading helpers
    // -------------------------------------------------------------------------

    private void LoadModels(string basePath)
    {
        var priceDir = Path.Combine(basePath, "Price");
        if (!Directory.Exists(priceDir)) return;

        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(priceDir, "*_model_*.onnx"))
        {
            var stem = Path.GetFileNameWithoutExtension(file); // {symbol}_model_{i}
            var parts = stem.Split('_');
            if (parts.Length < 3) continue;

            // parts[^2] = "model", parts[^1] = index, parts[0..^2] = symbol (handles underscores in tickers)
            var sym = string.Join("_", parts[0..^2]);
            if (!grouped.TryGetValue(sym, out var list))
            {
                list = [];
                grouped[sym] = list;
            }
            list.Add(file);
        }

        foreach (var (sym, paths) in grouped)
        {
            try
            {
                var sessions = paths
                    .Order()
                    .Select(p => new InferenceSession(p))
                    .ToArray();
                _models[sym] = sessions;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load ONNX model(s) for symbol '{Symbol}'.", sym);
            }
        }
    }

    private void LoadCorrelation(string basePath)
    {
        var jsonPath = Path.Combine(basePath, "correlation.json");
        if (!File.Exists(jsonPath)) return;

        try
        {
            var json = File.ReadAllText(jsonPath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, float>>>(json);
            if (parsed is null) return;

            foreach (var (sym, row) in parsed)
                _correlation[sym] = new Dictionary<string, float>(row, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load correlation matrix from '{Path}'.", jsonPath);
        }
    }

    public void Dispose()
    {
        foreach (var sessions in _models.Values)
            foreach (var s in sessions)
                s.Dispose();
    }
}
