using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpCallGraphAnalyzer.Models;
using CSharpCallGraphAnalyzer.Configuration;

namespace CSharpCallGraphAnalyzer.Analysis;

/// <summary>
/// Discovers all methods in a compilation
/// </summary>
public class MethodDiscovery
{
    private readonly AnalysisOptions _options;

    public MethodDiscovery(AnalysisOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Discover all methods in the given compilations
    /// </summary>
    public async Task<List<Models.MethodInfo>> DiscoverMethodsAsync(
        List<Compilation> compilations,
        CancellationToken cancellationToken = default)
    {
        var allMethods = new List<Models.MethodInfo>();

        foreach (var compilation in compilations)
        {
            var methods = await DiscoverMethodsInCompilationAsync(compilation, cancellationToken);
            allMethods.AddRange(methods);
        }

        Console.Error.WriteLine($"Discovered {allMethods.Count} method(s)");
        return allMethods;
    }

    /// <summary>
    /// Discover methods in a single compilation
    /// </summary>
    private async Task<List<Models.MethodInfo>> DiscoverMethodsInCompilationAsync(
        Compilation compilation,
        CancellationToken cancellationToken = default)
    {
        var methods = new List<Models.MethodInfo>();

        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            // Check if file should be excluded
            if (ShouldExcludeFile(syntaxTree.FilePath))
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Find all method declarations
            var methodDeclarations = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>();

            foreach (var methodDecl in methodDeclarations)
            {
                var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, cancellationToken);
                if (methodSymbol != null)
                {
                    var methodInfo = CreateMethodInfo(methodSymbol, syntaxTree.FilePath);
                    if (methodInfo != null)
                    {
                        methods.Add(methodInfo);
                    }
                }
            }

            // Also find constructors
            var constructorDeclarations = root.DescendantNodes()
                .OfType<ConstructorDeclarationSyntax>();

            foreach (var ctorDecl in constructorDeclarations)
            {
                var ctorSymbol = semanticModel.GetDeclaredSymbol(ctorDecl, cancellationToken);
                if (ctorSymbol != null)
                {
                    var methodInfo = CreateMethodInfo(ctorSymbol, syntaxTree.FilePath);
                    if (methodInfo != null)
                    {
                        methods.Add(methodInfo);
                    }
                }
            }

            // Find property accessors (getters/setters)
            var propertyDeclarations = root.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>();

            foreach (var propDecl in propertyDeclarations)
            {
                var propSymbol = semanticModel.GetDeclaredSymbol(propDecl, cancellationToken);
                if (propSymbol != null)
                {
                    if (propSymbol.GetMethod != null)
                    {
                        var getterInfo = CreateMethodInfo(propSymbol.GetMethod, syntaxTree.FilePath);
                        if (getterInfo != null)
                        {
                            methods.Add(getterInfo);
                        }
                    }

                    if (propSymbol.SetMethod != null)
                    {
                        var setterInfo = CreateMethodInfo(propSymbol.SetMethod, syntaxTree.FilePath);
                        if (setterInfo != null)
                        {
                            methods.Add(setterInfo);
                        }
                    }
                }
            }
        }

        return methods;
    }

    /// <summary>
    /// Create MethodInfo from IMethodSymbol
    /// </summary>
    private Models.MethodInfo? CreateMethodInfo(IMethodSymbol methodSymbol, string filePath)
    {
        // Check if method should be excluded based on namespace
        var containingNamespace = methodSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ShouldExcludeNamespace(containingNamespace))
        {
            return null;
        }

        // Get location information
        var location = methodSymbol.Locations.FirstOrDefault();
        var lineNumber = 0;
        if (location?.IsInSource == true)
        {
            var lineSpan = location.GetLineSpan();
            lineNumber = lineSpan.StartLinePosition.Line + 1;
        }

        // Get attributes
        var attributes = methodSymbol.GetAttributes()
            .Select(attr => attr.AttributeClass?.ToDisplayString() ?? string.Empty)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

        // Generate unique ID
        var id = GenerateMethodId(methodSymbol);

        var methodInfo = new Models.MethodInfo
        {
            Id = id,
            Name = methodSymbol.Name,
            FullName = methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            Namespace = containingNamespace,
            ClassName = methodSymbol.ContainingType?.Name ?? string.Empty,
            Accessibility = methodSymbol.DeclaredAccessibility.ToString().ToLowerInvariant(),
            IsStatic = methodSymbol.IsStatic,
            IsAbstract = methodSymbol.IsAbstract,
            IsVirtual = methodSymbol.IsVirtual,
            IsOverride = methodSymbol.IsOverride,
            FilePath = filePath,
            LineNumber = lineNumber,
            Signature = GetMethodSignature(methodSymbol),
            Symbol = methodSymbol,
            Attributes = attributes
        };

        return methodInfo;
    }

    /// <summary>
    /// Generate a unique ID for a method
    /// </summary>
    private string GenerateMethodId(IMethodSymbol methodSymbol)
    {
        // Use the full symbol string as a unique identifier
        var fullName = methodSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Create a hash for shorter IDs
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(fullName));
        var hash = Convert.ToBase64String(hashBytes).Replace("/", "_").Replace("+", "-").Substring(0, 16);

        return $"method_{hash}";
    }

    /// <summary>
    /// Get a readable method signature
    /// </summary>
    private string GetMethodSignature(IMethodSymbol methodSymbol)
    {
        return methodSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
    }

    /// <summary>
    /// Check if a file should be excluded
    /// </summary>
    private bool ShouldExcludeFile(string filePath)
    {
        foreach (var pattern in _options.ExcludeFilePatterns)
        {
            if (filePath.Contains(pattern.Replace("**", "").Replace("*", ""), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Always exclude bin and obj directories
        if (filePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            filePath.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Check if a namespace should be excluded
    /// </summary>
    private bool ShouldExcludeNamespace(string namespaceName)
    {
        foreach (var pattern in _options.ExcludeNamespaces)
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
