using System.Text.Json;

namespace CSharpCallGraphAnalyzer.Configuration;

/// <summary>
/// Loads configuration from .csharp-analyzer.json file
/// </summary>
public class ConfigurationLoader
{
    private const string ConfigFileName = ".csharp-analyzer.json";

    /// <summary>
    /// Load configuration from file if it exists, otherwise return default options
    /// </summary>
    public static AnalysisOptions LoadConfiguration(string? solutionPath = null, AnalysisOptions? baseOptions = null)
    {
        var options = baseOptions ?? new AnalysisOptions();

        // Find config file
        string? configPath = FindConfigFile(solutionPath);

        if (configPath == null)
        {
            Console.Error.WriteLine($"No {ConfigFileName} found, using default configuration");
            return options;
        }

        try
        {
            Console.Error.WriteLine($"Loading configuration from: {configPath}");
            var configJson = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<ConfigurationFile>(configJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (config == null)
            {
                Console.Error.WriteLine("Warning: Config file is empty or invalid, using defaults");
                return options;
            }

            // Apply configuration to options (only override if not already set via CLI)
            ApplyConfiguration(options, config);

            Console.Error.WriteLine($"Configuration loaded successfully");
            return options;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to load configuration: {ex.Message}");
            Console.Error.WriteLine("Using default configuration");
            return options;
        }
    }

    /// <summary>
    /// Find the configuration file starting from solution directory
    /// </summary>
    private static string? FindConfigFile(string? solutionPath)
    {
        // If solution path provided, look in that directory
        if (!string.IsNullOrEmpty(solutionPath))
        {
            var solutionDir = Path.GetDirectoryName(Path.GetFullPath(solutionPath));
            if (solutionDir != null)
            {
                var configPath = Path.Combine(solutionDir, ConfigFileName);
                if (File.Exists(configPath))
                {
                    return configPath;
                }
            }
        }

        // Look in current directory
        var currentDirConfig = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
        if (File.Exists(currentDirConfig))
        {
            return currentDirConfig;
        }

        // Search up the directory tree
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null)
        {
            var configPath = Path.Combine(currentDir, ConfigFileName);
            if (File.Exists(configPath))
            {
                return configPath;
            }

            var parentDir = Directory.GetParent(currentDir);
            currentDir = parentDir?.FullName;
        }

        return null;
    }

    /// <summary>
    /// Apply configuration file settings to options (only if not already set)
    /// </summary>
    private static void ApplyConfiguration(AnalysisOptions options, ConfigurationFile config)
    {
        // Entry point attributes
        if (config.EntryPointAttributes?.Length > 0)
        {
            options.EntryPointAttributes = config.EntryPointAttributes;
        }

        // Exclude namespaces (merge with CLI args)
        if (config.ExcludeNamespaces?.Length > 0)
        {
            var existing = options.ExcludeNamespaces ?? Array.Empty<string>();
            options.ExcludeNamespaces = existing.Concat(config.ExcludeNamespaces).Distinct().ToArray();
        }

        // Exclude file patterns
        if (config.ExcludeFilePatterns?.Length > 0)
        {
            var existing = options.ExcludeFilePatterns ?? Array.Empty<string>();
            options.ExcludeFilePatterns = existing.Concat(config.ExcludeFilePatterns).Distinct().ToArray();
        }

        // Always used namespaces
        if (config.AlwaysUsedNamespaces?.Length > 0)
        {
            options.AlwaysUsedNamespaces = config.AlwaysUsedNamespaces;
        }

        // Minimum accessibility
        if (!string.IsNullOrEmpty(config.MinimumAccessibility))
        {
            options.MinimumAccessibility = config.MinimumAccessibility;
        }

        // Reflection detection
        if (config.ReflectionPatterns?.Enabled != null)
        {
            options.DetectReflection = config.ReflectionPatterns.Enabled.Value;
        }

        // DI detection
        if (config.DependencyInjection?.Enabled != null)
        {
            options.DetectDependencyInjection = config.DependencyInjection.Enabled.Value;
        }

        // Caching
        if (config.Caching?.Enabled != null)
        {
            options.EnableCaching = config.Caching.Enabled.Value;
        }

        if (!string.IsNullOrEmpty(config.Caching?.CacheDirectory))
        {
            options.CacheDirectory = config.Caching.CacheDirectory;
        }
    }
}

/// <summary>
/// Configuration file schema
/// </summary>
public class ConfigurationFile
{
    public string? Version { get; set; }
    public string[]? EntryPointAttributes { get; set; }
    public string[]? ExcludeNamespaces { get; set; }
    public string[]? ExcludeFilePatterns { get; set; }
    public string[]? AlwaysUsedNamespaces { get; set; }
    public string? MinimumAccessibility { get; set; }
    public ReflectionPatternsConfig? ReflectionPatterns { get; set; }
    public DependencyInjectionConfig? DependencyInjection { get; set; }
    public CachingConfig? Caching { get; set; }
}

public class ReflectionPatternsConfig
{
    public bool? Enabled { get; set; }
    public string[]? MethodNamePatterns { get; set; }
}

public class DependencyInjectionConfig
{
    public bool? Enabled { get; set; }
    public string[]? RegistrationPatterns { get; set; }
}

public class CachingConfig
{
    public bool? Enabled { get; set; }
    public string? CacheDirectory { get; set; }
}
