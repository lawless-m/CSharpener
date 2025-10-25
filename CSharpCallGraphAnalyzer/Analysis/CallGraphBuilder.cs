using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpCallGraphAnalyzer.Models;
using CSharpCallGraphAnalyzer.Utilities;

namespace CSharpCallGraphAnalyzer.Analysis;

/// <summary>
/// Builds a call graph by analyzing method invocations
/// </summary>
public class CallGraphBuilder
{
    private readonly Dictionary<IMethodSymbol, string> _methodToIdMap;

    public CallGraphBuilder()
    {
        _methodToIdMap = new Dictionary<IMethodSymbol, string>(SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// Build a call graph from discovered methods
    /// </summary>
    public async Task<CallGraph> BuildCallGraphAsync(
        List<Models.MethodInfo> methods,
        List<Compilation> compilations,
        CancellationToken cancellationToken = default)
    {
        var callGraph = new CallGraph();

        // First, add all methods to the graph and build symbol lookup
        foreach (var method in methods)
        {
            callGraph.AddMethod(method);

            if (method.Symbol != null)
            {
                _methodToIdMap[method.Symbol] = method.Id;
            }
        }

        Console.Error.WriteLine($"Building call graph for {methods.Count} method(s)...");

        // Now analyze method bodies to find invocations
        foreach (var compilation in compilations)
        {
            await AnalyzeCompilationAsync(callGraph, compilation, cancellationToken);
        }

        Console.Error.WriteLine($"Call graph built with {callGraph.Calls.Sum(c => c.Value.Count)} call(s)");

        return callGraph;
    }

    /// <summary>
    /// Analyze a compilation to find method calls
    /// </summary>
    private async Task AnalyzeCompilationAsync(
        CallGraph callGraph,
        Compilation compilation,
        CancellationToken cancellationToken = default)
    {
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Find all method declarations to analyze their bodies
            var methodDeclarations = root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>();

            foreach (var methodDecl in methodDeclarations)
            {
                var callerSymbol = semanticModel.GetDeclaredSymbol(methodDecl, cancellationToken);
                if (callerSymbol == null || !_methodToIdMap.ContainsKey(callerSymbol))
                {
                    continue;
                }

                var callerId = _methodToIdMap[callerSymbol];

                // Analyze the method body for invocations
                AnalyzeMethodBody(callGraph, methodDecl, semanticModel, callerId, cancellationToken);
            }

            // Also analyze property accessors
            var propertyDeclarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();

            foreach (var propDecl in propertyDeclarations)
            {
                var propSymbol = semanticModel.GetDeclaredSymbol(propDecl, cancellationToken);
                if (propSymbol == null)
                {
                    continue;
                }

                // Analyze getter
                if (propSymbol.GetMethod != null && _methodToIdMap.ContainsKey(propSymbol.GetMethod))
                {
                    var getterId = _methodToIdMap[propSymbol.GetMethod];
                    if (propDecl.AccessorList != null)
                    {
                        var getter = propDecl.AccessorList.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
                        if (getter != null)
                        {
                            AnalyzeMethodBody(callGraph, getter, semanticModel, getterId, cancellationToken);
                        }
                    }
                }

                // Analyze setter
                if (propSymbol.SetMethod != null && _methodToIdMap.ContainsKey(propSymbol.SetMethod))
                {
                    var setterId = _methodToIdMap[propSymbol.SetMethod];
                    if (propDecl.AccessorList != null)
                    {
                        var setter = propDecl.AccessorList.Accessors.FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
                        if (setter != null)
                        {
                            AnalyzeMethodBody(callGraph, setter, semanticModel, setterId, cancellationToken);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Analyze a method body to find all invocations
    /// </summary>
    private void AnalyzeMethodBody(
        CallGraph callGraph,
        SyntaxNode methodNode,
        SemanticModel semanticModel,
        string callerId,
        CancellationToken cancellationToken)
    {
        // Find all invocation expressions
        var invocations = methodNode.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
            var calledMethod = symbolInfo.Symbol as IMethodSymbol;

            if (calledMethod != null && _methodToIdMap.TryGetValue(calledMethod, out var calleeId))
            {
                callGraph.AddCall(callerId, calleeId);
            }
        }

        // Find object creation expressions (constructor calls)
        var objectCreations = methodNode.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();

        foreach (var creation in objectCreations)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(creation, cancellationToken);
            var constructor = symbolInfo.Symbol as IMethodSymbol;

            if (constructor != null && _methodToIdMap.TryGetValue(constructor, out var calleeId))
            {
                callGraph.AddCall(callerId, calleeId);
            }
        }

        // Find property access (getter/setter usage)
        var memberAccesses = methodNode.DescendantNodes().OfType<MemberAccessExpressionSyntax>();

        foreach (var memberAccess in memberAccesses)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess, cancellationToken);

            if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
            {
                // Determine if it's a read (getter) or write (setter)
                var parent = memberAccess.Parent;

                // Check if this is on the left side of an assignment (setter)
                if (parent is AssignmentExpressionSyntax assignment && assignment.Left == memberAccess)
                {
                    if (propertySymbol.SetMethod != null && _methodToIdMap.TryGetValue(propertySymbol.SetMethod, out var setterId))
                    {
                        callGraph.AddCall(callerId, setterId);
                    }
                }
                else
                {
                    // Getter usage
                    if (propertySymbol.GetMethod != null && _methodToIdMap.TryGetValue(propertySymbol.GetMethod, out var getterId))
                    {
                        callGraph.AddCall(callerId, getterId);
                    }
                }
            }
        }

        // Find simple identifier references (could be properties or methods)
        var identifiers = methodNode.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifiers)
        {
            // Skip if it's already part of a member access we've processed
            if (identifier.Parent is MemberAccessExpressionSyntax)
            {
                continue;
            }

            var symbolInfo = semanticModel.GetSymbolInfo(identifier, cancellationToken);

            if (symbolInfo.Symbol is IPropertySymbol propertySymbol)
            {
                // Property getter usage
                if (propertySymbol.GetMethod != null && _methodToIdMap.TryGetValue(propertySymbol.GetMethod, out var getterId))
                {
                    callGraph.AddCall(callerId, getterId);
                }
            }
        }
    }
}
