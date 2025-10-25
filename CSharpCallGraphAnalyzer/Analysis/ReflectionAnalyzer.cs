using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpCallGraphAnalyzer.Models;

namespace CSharpCallGraphAnalyzer.Analysis;

/// <summary>
/// Detects reflection usage that might call methods dynamically
/// </summary>
public class ReflectionAnalyzer
{
    /// <summary>
    /// Scan compilations for reflection usage and generate warnings
    /// </summary>
    public List<AnalysisWarning> DetectReflectionUsage(
        List<Compilation> compilations,
        CallGraph callGraph)
    {
        var warnings = new List<AnalysisWarning>();

        Console.Error.WriteLine("Scanning for reflection usage...");

        foreach (var compilation in compilations)
        {
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // Find reflection method calls
                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

                foreach (var invocation in invocations)
                {
                    var warning = AnalyzeInvocation(invocation, semanticModel, syntaxTree.FilePath, callGraph);
                    if (warning != null)
                    {
                        warnings.Add(warning);
                    }
                }
            }
        }

        Console.Error.WriteLine($"Found {warnings.Count} reflection warning(s)");
        return warnings;
    }

    /// <summary>
    /// Analyze a single invocation for reflection patterns
    /// </summary>
    private AnalysisWarning? AnalyzeInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string filePath,
        CallGraph callGraph)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        var method = symbolInfo.Symbol as IMethodSymbol;

        if (method == null)
            return null;

        var methodName = method.Name;
        var containingType = method.ContainingType?.ToDisplayString();

        // Check for reflection API calls
        var reflectionPatterns = new Dictionary<string, string>
        {
            { "Type.GetMethod", "Type.GetMethod() call - method may be invoked via reflection" },
            { "Type.GetMethods", "Type.GetMethods() call - methods may be invoked via reflection" },
            { "Type.GetProperty", "Type.GetProperty() call - property may be accessed via reflection" },
            { "Type.GetProperties", "Type.GetProperties() call - properties may be accessed via reflection" },
            { "MethodInfo.Invoke", "MethodInfo.Invoke() call - method invoked dynamically" },
            { "PropertyInfo.GetValue", "PropertyInfo.GetValue() call - property accessed dynamically" },
            { "PropertyInfo.SetValue", "PropertyInfo.SetValue() call - property set dynamically" },
            { "Activator.CreateInstance", "Activator.CreateInstance() call - type instantiated dynamically" },
            { "Assembly.CreateInstance", "Assembly.CreateInstance() call - type created via reflection" }
        };

        // Check if this is a known reflection method
        string? reflectionType = null;
        foreach (var pattern in reflectionPatterns)
        {
            if (containingType?.Contains(pattern.Key.Split('.')[0]) == true &&
                methodName == pattern.Key.Split('.')[1])
            {
                reflectionType = pattern.Value;
                break;
            }
        }

        if (reflectionType == null)
            return null;

        // Try to extract method/type names from string literals
        var arguments = invocation.ArgumentList.Arguments;
        var targetNames = new List<string>();

        foreach (var arg in arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var value = literal.Token.ValueText;
                if (!string.IsNullOrEmpty(value))
                {
                    targetNames.Add(value);
                }
            }
        }

        // Try to find methods that match the string literals
        var affectedMethods = new List<string>();
        if (targetNames.Any())
        {
            foreach (var name in targetNames)
            {
                var matches = callGraph.Methods.Values
                    .Where(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                               m.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .Take(5);

                foreach (var match in matches)
                {
                    affectedMethods.Add(match.Id);

                    // Reduce confidence for this method since it might be called via reflection
                    if (match.Confidence == ConfidenceLevel.High)
                    {
                        match.Confidence = ConfidenceLevel.Medium;
                        match.Reason = $"Found in reflection call at {filePath}; {match.Reason}";
                    }
                }
            }
        }

        var location = invocation.GetLocation();
        var lineNumber = location.GetLineSpan().StartLinePosition.Line + 1;

        var message = reflectionType;
        if (targetNames.Any())
        {
            message += $" - targets: {string.Join(", ", targetNames)}";
        }
        if (affectedMethods.Any())
        {
            message += $" - found {affectedMethods.Count} matching method(s)";
        }

        return new AnalysisWarning
        {
            Type = "possibleReflectionUsage",
            Message = message,
            FilePath = filePath,
            LineNumber = lineNumber,
            MethodId = affectedMethods.FirstOrDefault()
        };
    }
}
