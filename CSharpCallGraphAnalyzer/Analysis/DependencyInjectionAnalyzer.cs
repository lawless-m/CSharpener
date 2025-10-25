using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpCallGraphAnalyzer.Models;

namespace CSharpCallGraphAnalyzer.Analysis;

/// <summary>
/// Detects dependency injection registrations that mark types as used
/// </summary>
public class DependencyInjectionAnalyzer
{
    /// <summary>
    /// Scan for DI registrations and mark registered types as used
    /// </summary>
    public List<AnalysisWarning> DetectDependencyInjectionUsage(
        List<Compilation> compilations,
        CallGraph callGraph)
    {
        var warnings = new List<AnalysisWarning>();
        var registeredTypes = new HashSet<string>();

        Console.Error.WriteLine("Scanning for dependency injection registrations...");

        foreach (var compilation in compilations)
        {
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                var root = syntaxTree.GetRoot();

                // Find DI registration calls
                var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

                foreach (var invocation in invocations)
                {
                    var registrations = AnalyzeInvocation(invocation, semanticModel, syntaxTree.FilePath);
                    foreach (var (typeName, warning) in registrations)
                    {
                        registeredTypes.Add(typeName);
                        if (warning != null)
                        {
                            warnings.Add(warning);
                        }
                    }
                }
            }
        }

        // Mark all methods in registered types as used
        int markedCount = 0;
        foreach (var method in callGraph.Methods.Values)
        {
            var fullTypeName = $"{method.Namespace}.{method.ClassName}";

            foreach (var registeredType in registeredTypes)
            {
                if (fullTypeName.Contains(registeredType, StringComparison.OrdinalIgnoreCase) ||
                    method.ClassName.Equals(registeredType, StringComparison.OrdinalIgnoreCase))
                {
                    if (!method.IsEntryPoint)
                    {
                        method.IsEntryPoint = true;
                        markedCount++;
                    }

                    // Reduce confidence if marked as unused
                    if (!method.IsUsed && method.Confidence == ConfidenceLevel.High)
                    {
                        method.Confidence = ConfidenceLevel.Low;
                        method.Reason = $"Type registered in DI container; {method.Reason}";
                    }
                    break;
                }
            }
        }

        Console.Error.WriteLine($"Found {registeredTypes.Count} DI registration(s), marked {markedCount} method(s) as entry points");

        return warnings;
    }

    /// <summary>
    /// Analyze invocation for DI registration patterns
    /// </summary>
    private List<(string TypeName, AnalysisWarning? Warning)> AnalyzeInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        string filePath)
    {
        var results = new List<(string, AnalysisWarning?)>();

        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        var method = symbolInfo.Symbol as IMethodSymbol;

        if (method == null)
            return results;

        var methodName = method.Name;

        // Check for common DI registration patterns
        var diPatterns = new[]
        {
            "AddTransient", "AddScoped", "AddSingleton",
            "TryAddTransient", "TryAddScoped", "TryAddSingleton",
            "RegisterType", "Register", "RegisterInstance",
            "For", "Use", "Bind"
        };

        if (!diPatterns.Any(p => methodName.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            return results;
        }

        // Extract type arguments (generic parameters)
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Name is GenericNameSyntax genericName)
            {
                foreach (var typeArg in genericName.TypeArgumentList.Arguments)
                {
                    var typeInfo = semanticModel.GetTypeInfo(typeArg);
                    if (typeInfo.Type != null)
                    {
                        var typeName = typeInfo.Type.Name;
                        var fullTypeName = typeInfo.Type.ToDisplayString();

                        results.Add((typeName, CreateWarning(methodName, fullTypeName, filePath, invocation)));
                    }
                }
            }
        }

        // Also check method arguments for type expressions
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is TypeOfExpressionSyntax typeOfExpr)
            {
                var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
                if (typeInfo.Type != null)
                {
                    var typeName = typeInfo.Type.Name;
                    var fullTypeName = typeInfo.Type.ToDisplayString();

                    results.Add((typeName, CreateWarning(methodName, fullTypeName, filePath, invocation)));
                }
            }
        }

        return results;
    }

    private AnalysisWarning? CreateWarning(
        string registrationMethod,
        string typeName,
        string filePath,
        InvocationExpressionSyntax invocation)
    {
        var location = invocation.GetLocation();
        var lineNumber = location.GetLineSpan().StartLinePosition.Line + 1;

        return new AnalysisWarning
        {
            Type = "dependencyInjectionRegistration",
            Message = $"{registrationMethod}<{typeName}>() - Type registered in DI container",
            FilePath = filePath,
            LineNumber = lineNumber
        };
    }
}
