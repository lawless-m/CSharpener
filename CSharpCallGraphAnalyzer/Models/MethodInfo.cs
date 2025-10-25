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

    // === Documentation Support Properties ===

    /// <summary>
    /// Detailed parameter information for documentation generation
    /// </summary>
    public List<ParameterInfo> Parameters { get; set; } = new();

    /// <summary>
    /// Return type full name (e.g., "System.Threading.Tasks.Task<string>")
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// Return type display name (e.g., "Task<string>")
    /// </summary>
    public string ReturnTypeDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is an async method
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// Generic type parameters (e.g., ["T", "TResult"])
    /// </summary>
    public List<string> GenericParameters { get; set; } = new();

    /// <summary>
    /// Generic type parameter constraints (e.g., "where T : class, IDisposable")
    /// </summary>
    public List<string> GenericConstraints { get; set; } = new();

    /// <summary>
    /// Existing XML documentation comment (if any)
    /// </summary>
    public string? ExistingDocumentation { get; set; }

    /// <summary>
    /// Interfaces implemented by the containing class
    /// </summary>
    public List<string> ClassInterfaces { get; set; } = new();

    /// <summary>
    /// Base class of the containing class
    /// </summary>
    public string? ClassBaseType { get; set; }

    /// <summary>
    /// Whether the containing class is abstract
    /// </summary>
    public bool ClassIsAbstract { get; set; }

    /// <summary>
    /// Whether the containing class is sealed
    /// </summary>
    public bool ClassIsSealed { get; set; }

    /// <summary>
    /// Whether the containing class is static
    /// </summary>
    public bool ClassIsStatic { get; set; }

    /// <summary>
    /// Number of lines of code in the method body (approximate)
    /// </summary>
    public int LinesOfCode { get; set; }

    /// <summary>
    /// Whether this method implements an interface method
    /// </summary>
    public bool ImplementsInterface { get; set; }

    /// <summary>
    /// Interface method this implements (if applicable)
    /// </summary>
    public string? ImplementedInterfaceMethod { get; set; }
}
