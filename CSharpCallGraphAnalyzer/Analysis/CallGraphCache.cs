using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.Models;

namespace CSharpCallGraphAnalyzer.Analysis;

/// <summary>
/// Caches call graph analysis results for performance
/// </summary>
public class CallGraphCache
{
    private readonly AnalysisOptions _options;
    private readonly string _cacheDirectory;

    public CallGraphCache(AnalysisOptions options)
    {
        _options = options;
        _cacheDirectory = Path.Combine(
            Path.GetDirectoryName(_options.SolutionPath) ?? Directory.GetCurrentDirectory(),
            _options.CacheDirectory
        );
    }

    /// <summary>
    /// Try to load cached call graph if valid
    /// </summary>
    public async Task<CachedCallGraph?> TryLoadCacheAsync(Solution solution)
    {
        if (!_options.EnableCaching)
        {
            return null;
        }

        try
        {
            var cacheKey = await GenerateCacheKeyAsync(solution);
            var cacheFile = GetCacheFilePath(cacheKey);

            if (!File.Exists(cacheFile))
            {
                Console.Error.WriteLine("No cache found");
                return null;
            }

            Console.Error.WriteLine($"Loading cache from: {cacheFile}");
            var json = await File.ReadAllTextAsync(cacheFile);
            var cached = JsonSerializer.Deserialize<CachedCallGraph>(json);

            if (cached == null)
            {
                Console.Error.WriteLine("Cache file is invalid");
                return null;
            }

            // Validate cache key matches
            if (cached.CacheKey != cacheKey)
            {
                Console.Error.WriteLine("Cache is stale (files have changed)");
                return null;
            }

            Console.Error.WriteLine($"✓ Cache hit! Using cached analysis from {cached.Timestamp}");
            return cached;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load cache: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save call graph to cache
    /// </summary>
    public async Task SaveCacheAsync(Solution solution, CallGraph callGraph, List<string> entryPoints)
    {
        if (!_options.EnableCaching)
        {
            return;
        }

        try
        {
            // Ensure cache directory exists
            Directory.CreateDirectory(_cacheDirectory);

            var cacheKey = await GenerateCacheKeyAsync(solution);
            var cacheFile = GetCacheFilePath(cacheKey);

            var cached = new CachedCallGraph
            {
                CacheKey = cacheKey,
                Timestamp = DateTime.UtcNow,
                SolutionPath = _options.SolutionPath,
                CallGraph = callGraph,
                EntryPoints = entryPoints
            };

            var json = JsonSerializer.Serialize(cached, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            await File.WriteAllTextAsync(cacheFile, json);
            Console.Error.WriteLine($"✓ Cache saved to: {cacheFile}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to save cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate cache key based on solution path and file modification times
    /// </summary>
    private Task<string> GenerateCacheKeyAsync(Solution solution)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(_options.SolutionPath);
        keyBuilder.Append('|');

        // Get all C# files and their modification times
        var files = new List<(string Path, DateTime Modified)>();

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath != null && File.Exists(document.FilePath))
                {
                    var lastModified = File.GetLastWriteTimeUtc(document.FilePath);
                    files.Add((document.FilePath, lastModified));
                }
            }
        }

        // Sort for consistency
        files.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));

        // Include file paths and modification times in key
        foreach (var (path, modified) in files)
        {
            keyBuilder.Append(path);
            keyBuilder.Append(':');
            keyBuilder.Append(modified.Ticks);
            keyBuilder.Append('|');
        }

        // Hash the key for reasonable filename length
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyBuilder.ToString()));
        var cacheKey = Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").Replace("=", "");
        return Task.FromResult(cacheKey);
    }

    /// <summary>
    /// Get cache file path for a given key
    /// </summary>
    private string GetCacheFilePath(string cacheKey)
    {
        return Path.Combine(_cacheDirectory, $"callgraph_{cacheKey}.json");
    }

    /// <summary>
    /// Clear all cache files
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
                Console.Error.WriteLine($"Cache cleared: {_cacheDirectory}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to clear cache: {ex.Message}");
        }
    }
}

/// <summary>
/// Cached call graph data
/// </summary>
public class CachedCallGraph
{
    public string CacheKey { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string SolutionPath { get; set; } = string.Empty;
    public CallGraph CallGraph { get; set; } = new();
    public List<string> EntryPoints { get; set; } = new();
}
