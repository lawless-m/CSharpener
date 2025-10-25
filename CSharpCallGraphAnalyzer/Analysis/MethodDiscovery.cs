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
        var linesOfCode = 0;
        if (location?.IsInSource == true)
        {
            var lineSpan = location.GetLineSpan();
            lineNumber = lineSpan.StartLinePosition.Line + 1;
            // Calculate lines of code (end line - start line)
            linesOfCode = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
        }

        // Get attributes
        var attributes = methodSymbol.GetAttributes()
            .Select(attr => attr.AttributeClass?.ToDisplayString() ?? string.Empty)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

        // Generate unique ID
        var id = GenerateMethodId(methodSymbol);

        // Extract parameters
        var parameters = ExtractParameters(methodSymbol);

        // Extract return type
        var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var returnTypeDisplayName = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);

        // Check if async
        var isAsync = methodSymbol.IsAsync ||
                      returnType.Contains("System.Threading.Tasks.Task") ||
                      returnType.Contains("System.Threading.Tasks.ValueTask");

        // Extract generic parameters
        var genericParameters = methodSymbol.TypeParameters
            .Select(tp => tp.Name)
            .ToList();

        var genericConstraints = methodSymbol.TypeParameters
            .Where(tp => tp.HasConstructorConstraint || tp.HasReferenceTypeConstraint ||
                        tp.HasValueTypeConstraint || tp.ConstraintTypes.Length > 0)
            .Select(tp => FormatGenericConstraint(tp))
            .ToList();

        // Extract XML documentation
        var xmlDoc = methodSymbol.GetDocumentationCommentXml();
        string? existingDoc = string.IsNullOrWhiteSpace(xmlDoc) ? null : xmlDoc;

        // Extract class context
        var containingType = methodSymbol.ContainingType;
        var classInterfaces = containingType?.AllInterfaces
            .Select(i => i.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat))
            .ToList() ?? new List<string>();

        var classBaseType = containingType?.BaseType?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        // Don't include "object" as base type - it's implicit
        if (classBaseType == "object")
        {
            classBaseType = null;
        }

        // Check if implements interface
        var implementsInterface = false;
        string? implementedInterfaceMethod = null;
        if (containingType != null)
        {
            foreach (var iface in containingType.AllInterfaces)
            {
                var interfaceMethod = iface.GetMembers()
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(m => containingType.FindImplementationForInterfaceMember(m)?.Equals(methodSymbol, SymbolEqualityComparer.Default) == true);

                if (interfaceMethod != null)
                {
                    implementsInterface = true;
                    implementedInterfaceMethod = $"{iface.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}.{interfaceMethod.Name}";
                    break;
                }
            }
        }

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
            Attributes = attributes,

            // Documentation support properties
            Parameters = parameters,
            ReturnType = returnType,
            ReturnTypeDisplayName = returnTypeDisplayName,
            IsAsync = isAsync,
            GenericParameters = genericParameters,
            GenericConstraints = genericConstraints,
            ExistingDocumentation = existingDoc,
            ClassInterfaces = classInterfaces,
            ClassBaseType = classBaseType,
            ClassIsAbstract = containingType?.IsAbstract ?? false,
            ClassIsSealed = containingType?.IsSealed ?? false,
            ClassIsStatic = containingType?.IsStatic ?? false,
            LinesOfCode = linesOfCode,
            ImplementsInterface = implementsInterface,
            ImplementedInterfaceMethod = implementedInterfaceMethod
        };

        return methodInfo;
    }

    /// <summary>
    /// Extract detailed parameter information
    /// </summary>
    private List<Models.ParameterInfo> ExtractParameters(IMethodSymbol methodSymbol)
    {
        var parameters = new List<Models.ParameterInfo>();

        foreach (var param in methodSymbol.Parameters)
        {
            var paramInfo = new Models.ParameterInfo
            {
                Name = param.Name,
                Type = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                TypeDisplayName = param.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                HasDefaultValue = param.HasExplicitDefaultValue,
                DefaultValue = param.HasExplicitDefaultValue ? FormatDefaultValue(param.ExplicitDefaultValue) : null,
                IsRef = param.RefKind == RefKind.Ref,
                IsOut = param.RefKind == RefKind.Out,
                IsIn = param.RefKind == RefKind.In,
                IsParams = param.IsParams,
                IsOptional = param.IsOptional,
                Attributes = param.GetAttributes()
                    .Select(attr => attr.AttributeClass?.ToDisplayString() ?? string.Empty)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList()
            };

            parameters.Add(paramInfo);
        }

        return parameters;
    }

    /// <summary>
    /// Format a default value for display
    /// </summary>
    private string FormatDefaultValue(object? value)
    {
        if (value == null)
            return "null";

        if (value is string str)
            return $"\"{str}\"";

        if (value is bool b)
            return b ? "true" : "false";

        return value.ToString() ?? "null";
    }

    /// <summary>
    /// Format generic constraint for display
    /// </summary>
    private string FormatGenericConstraint(ITypeParameterSymbol typeParam)
    {
        var constraints = new List<string>();

        if (typeParam.HasReferenceTypeConstraint)
            constraints.Add("class");

        if (typeParam.HasValueTypeConstraint)
            constraints.Add("struct");

        if (typeParam.HasUnmanagedTypeConstraint)
            constraints.Add("unmanaged");

        foreach (var constraintType in typeParam.ConstraintTypes)
        {
            constraints.Add(constraintType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat));
        }

        if (typeParam.HasConstructorConstraint)
            constraints.Add("new()");

        return $"where {typeParam.Name} : {string.Join(", ", constraints)}";
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
