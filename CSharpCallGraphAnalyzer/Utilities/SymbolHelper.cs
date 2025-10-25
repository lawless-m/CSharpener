using Microsoft.CodeAnalysis;

namespace CSharpCallGraphAnalyzer.Utilities;

/// <summary>
/// Helper utilities for working with Roslyn symbols
/// </summary>
public static class SymbolHelper
{
    /// <summary>
    /// Get a display string for a symbol that can be used in output
    /// </summary>
    public static string GetDisplayString(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    /// <summary>
    /// Get the fully qualified name of a symbol
    /// </summary>
    public static string GetFullyQualifiedName(ISymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    /// <summary>
    /// Check if two symbols are equal using the default comparer
    /// </summary>
    public static bool AreEqual(ISymbol? symbol1, ISymbol? symbol2)
    {
        return SymbolEqualityComparer.Default.Equals(symbol1, symbol2);
    }

    /// <summary>
    /// Get the namespace of a symbol
    /// </summary>
    public static string GetNamespace(ISymbol symbol)
    {
        return symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
    }

    /// <summary>
    /// Check if a symbol has a specific attribute
    /// </summary>
    public static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString().Contains(attributeName) == true);
    }

    /// <summary>
    /// Get all attributes of a symbol as display strings
    /// </summary>
    public static List<string> GetAttributes(ISymbol symbol)
    {
        return symbol.GetAttributes()
            .Select(attr => attr.AttributeClass?.ToDisplayString() ?? string.Empty)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
    }
}
