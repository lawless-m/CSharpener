using Microsoft.CodeAnalysis;

namespace CSharpCallGraphAnalyzer.Models;

/// <summary>
/// Represents metadata about a method in the codebase
/// </summary>
public class MethodInfo
{
    /// <summary>
    /// Unique identifier for this method
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Simple method name (e.g., "CalculateDiscount")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full qualified name including namespace and type (e.g., "MyApp.Business.PricingService.CalculateDiscount")
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Namespace containing the method
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Type/class name containing the method
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Method accessibility (public, private, protected, internal)
    /// </summary>
    public string Accessibility { get; set; } = string.Empty;

    /// <summary>
    /// Whether the method is static
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Whether the method is abstract
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Whether the method is virtual
    /// </summary>
    public bool IsVirtual { get; set; }

    /// <summary>
    /// Whether the method is an override
    /// </summary>
    public bool IsOverride { get; set; }

    /// <summary>
    /// File path where the method is defined
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Line number where the method is defined
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Method signature (e.g., "decimal CalculateDiscount(decimal price, int quantity)")
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// The Roslyn IMethodSymbol for semantic analysis
    /// </summary>
    public IMethodSymbol? Symbol { get; set; }

    /// <summary>
    /// Whether this method has been marked as used in call graph traversal
    /// </summary>
    public bool IsUsed { get; set; }

    /// <summary>
    /// Whether this method is an entry point (Main, public API, attributed, etc.)
    /// </summary>
    public bool IsEntryPoint { get; set; }

    /// <summary>
    /// Attributes applied to this method
    /// </summary>
    public List<string> Attributes { get; set; } = new();

    /// <summary>
    /// Confidence level for unused status
    /// </summary>
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.High;

    /// <summary>
    /// Reason for the confidence level or unused status
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
