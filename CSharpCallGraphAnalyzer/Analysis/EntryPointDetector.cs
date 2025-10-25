using Microsoft.CodeAnalysis;
using CSharpCallGraphAnalyzer.Models;
using CSharpCallGraphAnalyzer.Configuration;

namespace CSharpCallGraphAnalyzer.Analysis;

/// <summary>
/// Detects entry points in the codebase (methods that should always be considered as used)
/// </summary>
public class EntryPointDetector
{
    private readonly AnalysisOptions _options;

    public EntryPointDetector(AnalysisOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Identify all entry points in the call graph
    /// </summary>
    public List<string> DetectEntryPoints(CallGraph callGraph)
    {
        var entryPoints = new List<string>();

        foreach (var method in callGraph.Methods.Values)
        {
            if (IsEntryPoint(method))
            {
                method.IsEntryPoint = true;
                entryPoints.Add(method.Id);
            }
        }

        Console.Error.WriteLine($"Detected {entryPoints.Count} entry point(s)");
        return entryPoints;
    }

    /// <summary>
    /// Determine if a method is an entry point
    /// </summary>
    private bool IsEntryPoint(Models.MethodInfo method)
    {
        // 1. Main method (program entry point)
        if (method.Name == "Main" && method.IsStatic)
        {
            return true;
        }

        // 2. Public methods in public types (potential API surface)
        // For now, we'll be conservative and only mark truly public API methods
        if (method.Accessibility == "public")
        {
            // Check if the namespace is in the "always used" list
            if (IsInAlwaysUsedNamespace(method.Namespace))
            {
                return true;
            }

            // Check if it has entry point attributes
            if (HasEntryPointAttribute(method))
            {
                return true;
            }

            // For MVP, we'll be conservative - public methods without attributes
            // are not automatically entry points unless in specific namespaces
            // This reduces false negatives
        }

        // 3. Methods with specific attributes (test methods, web endpoints, etc.)
        if (HasEntryPointAttribute(method))
        {
            return true;
        }

        // 4. Override methods (they might be called by framework)
        if (method.IsOverride)
        {
            return true;
        }

        // 5. Virtual methods in public types (might be overridden)
        if (method.IsVirtual && method.Accessibility == "public")
        {
            return true;
        }

        // 6. Interface implementations (might be called through interface)
        if (method.Symbol?.ContainingType?.AllInterfaces.Length > 0)
        {
            // Check if this method implements an interface method
            var implementedInterfaceMembers = method.Symbol.ContainingType
                .AllInterfaces
                .SelectMany(i => i.GetMembers())
                .OfType<IMethodSymbol>();

            foreach (var interfaceMethod in implementedInterfaceMembers)
            {
                var implementation = method.Symbol.ContainingType.FindImplementationForInterfaceMember(interfaceMethod);
                if (SymbolEqualityComparer.Default.Equals(implementation, method.Symbol))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a method has any entry point attributes
    /// </summary>
    private bool HasEntryPointAttribute(Models.MethodInfo method)
    {
        foreach (var attribute in method.Attributes)
        {
            // Check against configured entry point attributes
            foreach (var entryPointAttr in _options.EntryPointAttributes)
            {
                if (attribute.Contains(entryPointAttr, StringComparison.OrdinalIgnoreCase) ||
                    attribute.EndsWith("." + entryPointAttr, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a namespace is in the "always used" list
    /// </summary>
    private bool IsInAlwaysUsedNamespace(string namespaceName)
    {
        foreach (var pattern in _options.AlwaysUsedNamespaces)
        {
            if (IsPatternMatch(namespaceName, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Simple pattern matching with wildcards
    /// </summary>
    private bool IsPatternMatch(string text, string pattern)
    {
        if (pattern.Contains('*'))
        {
            var parts = pattern.Split('*');
            int currentIndex = 0;

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                int index = text.IndexOf(part, currentIndex, StringComparison.OrdinalIgnoreCase);
                if (index == -1)
                    return false;

                currentIndex = index + part.Length;
            }
            return true;
        }

        return text.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
