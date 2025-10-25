namespace CSharpCallGraphAnalyzer.Configuration;

/// <summary>
/// Options for configuring the analysis
/// </summary>
public class AnalysisOptions
{
    /// <summary>
    /// Path to solution or project file
    /// </summary>
    public string SolutionPath { get; set; } = string.Empty;

    /// <summary>
    /// Namespaces to exclude from analysis (supports wildcards like "*.Tests")
    /// </summary>
    public string[] ExcludeNamespaces { get; set; } = Array.Empty<string>();

    /// <summary>
    /// File patterns to exclude (supports glob patterns)
    /// </summary>
    public string[] ExcludeFilePatterns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Attribute names that mark methods as entry points
    /// </summary>
    public string[] EntryPointAttributes { get; set; } = GetDefaultEntryPointAttributes();

    /// <summary>
    /// Namespaces that should always be considered as used (e.g., public APIs)
    /// </summary>
    public string[] AlwaysUsedNamespaces { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Minimum accessibility level to analyze (Private, Protected, Internal, Public)
    /// </summary>
    public string MinimumAccessibility { get; set; } = "Private";

    /// <summary>
    /// Whether to include test methods in analysis
    /// </summary>
    public bool IncludeTests { get; set; } = true;

    /// <summary>
    /// Whether to detect potential reflection usage
    /// </summary>
    public bool DetectReflection { get; set; } = true;

    /// <summary>
    /// Whether to detect dependency injection patterns
    /// </summary>
    public bool DetectDependencyInjection { get; set; } = true;

    /// <summary>
    /// Output format (json, console, html, csv)
    /// </summary>
    public string OutputFormat { get; set; } = "json";

    /// <summary>
    /// Output file path (if not specified, outputs to stdout)
    /// </summary>
    public string? OutputFile { get; set; }

    /// <summary>
    /// Whether to include full call graph in output
    /// </summary>
    public bool IncludeCallGraph { get; set; } = false;

    /// <summary>
    /// Whether to enable caching
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache directory path
    /// </summary>
    public string CacheDirectory { get; set; } = ".csharp-analyzer-cache";

    /// <summary>
    /// Default entry point attributes to consider
    /// </summary>
    private static string[] GetDefaultEntryPointAttributes()
    {
        return new[]
        {
            // ASP.NET Core
            "Microsoft.AspNetCore.Mvc.HttpGetAttribute",
            "Microsoft.AspNetCore.Mvc.HttpPostAttribute",
            "Microsoft.AspNetCore.Mvc.HttpPutAttribute",
            "Microsoft.AspNetCore.Mvc.HttpDeleteAttribute",
            "Microsoft.AspNetCore.Mvc.HttpPatchAttribute",
            "Microsoft.AspNetCore.Mvc.RouteAttribute",

            // Testing frameworks
            "Xunit.FactAttribute",
            "Xunit.TheoryAttribute",
            "NUnit.Framework.TestAttribute",
            "NUnit.Framework.TestCaseAttribute",
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute",

            // Interop
            "System.Runtime.InteropServices.DllImportAttribute",

            // Serialization
            "System.Runtime.Serialization.DataMemberAttribute",
            "System.Text.Json.Serialization.JsonPropertyNameAttribute",
            "Newtonsoft.Json.JsonPropertyAttribute",
        };
    }
}

/// <summary>
/// Output format options
/// </summary>
public enum OutputFormat
{
    Json,
    Console,
    Html,
    Csv
}
