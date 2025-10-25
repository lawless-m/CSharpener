using System.CommandLine;
using System.CommandLine.Invocation;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.Output;

namespace CSharpCallGraphAnalyzer.Commands;

/// <summary>
/// Command to find all dependencies (methods called by) a specific method
/// </summary>
public class DependenciesCommand : Command
{
    public DependenciesCommand() : base("dependencies", "Find all methods called by a specific method")
    {
        var solutionOption = new Option<string>(
            aliases: new[] { "--solution", "-s" },
            description: "Path to the solution or project file")
        {
            IsRequired = true
        };

        var methodOption = new Option<string>(
            aliases: new[] { "--method", "-m" },
            description: "Fully qualified method name to search for")
        {
            IsRequired = true
        };

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            description: "Output format (json, console, dot, graphviz)",
            getDefaultValue: () => "json");

        AddOption(solutionOption);
        AddOption(methodOption);
        AddOption(formatOption);

        this.SetHandler(async (context) =>
        {
            var solution = context.ParseResult.GetValueForOption(solutionOption)!;
            var method = context.ParseResult.GetValueForOption(methodOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;

            var exitCode = await ExecuteAsync(solution, method, format, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
    }

    private async Task<int> ExecuteAsync(
        string solutionPath,
        string methodName,
        string format,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.Error.WriteLine($"Analyzing solution: {solutionPath}");
            Console.Error.WriteLine($"Finding dependencies of: {methodName}");

            var options = new AnalysisOptions
            {
                SolutionPath = solutionPath,
                OutputFormat = format
            };

            // Load solution
            var loader = new SolutionLoader(options);
            var solution = await loader.LoadAsync(cancellationToken);
            var compilations = await loader.GetCompilationsAsync(solution, cancellationToken);

            if (compilations.Count == 0)
            {
                Console.Error.WriteLine("Error: No compilations could be loaded");
                return 3;
            }

            // Discover methods
            var discovery = new MethodDiscovery(options);
            var methods = await discovery.DiscoverMethodsAsync(compilations, cancellationToken);

            // Build call graph
            var graphBuilder = new CallGraphBuilder();
            var callGraph = await graphBuilder.BuildCallGraphAsync(methods, compilations, cancellationToken);

            // Find the method
            var targetMethod = callGraph.Methods.Values.FirstOrDefault(m =>
                m.FullName.Contains(methodName, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));

            if (targetMethod == null)
            {
                Console.Error.WriteLine($"Error: Method '{methodName}' not found");
                Console.Error.WriteLine("\nSuggestions:");

                var similar = callGraph.Methods.Values
                    .Where(m => m.Name.Contains(methodName, StringComparison.OrdinalIgnoreCase))
                    .Take(5)
                    .ToList();

                if (similar.Any())
                {
                    foreach (var m in similar)
                    {
                        Console.Error.WriteLine($"  - {m.FullName}");
                    }
                }

                return 2;
            }

            // Get dependencies
            var dependencies = callGraph.GetCallees(targetMethod.Id).ToList();

            Console.Error.WriteLine($"Found {dependencies.Count} dependenc(ies)");

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonOutput.CreateDependenciesResult(callGraph, targetMethod.Id, targetMethod.FullName);
                Console.WriteLine(json);
            }
            else if (format.Equals("dot", StringComparison.OrdinalIgnoreCase) || format.Equals("graphviz", StringComparison.OrdinalIgnoreCase))
            {
                var dot = DotOutput.GenerateDependenciesDot(callGraph, targetMethod.Id, maxDepth: 3);
                Console.WriteLine(dot);
                Console.Error.WriteLine($"\nGenerate visualization with: dot -Tpng -o dependencies.png");
            }
            else
            {
                Console.WriteLine($"\nDependencies of: {targetMethod.FullName}");
                Console.WriteLine($"Location: {targetMethod.FilePath}:{targetMethod.LineNumber}");
                Console.WriteLine($"\nTotal dependencies: {dependencies.Count}");
                Console.WriteLine();

                if (dependencies.Count > 0)
                {
                    foreach (var dependencyId in dependencies)
                    {
                        if (callGraph.Methods.TryGetValue(dependencyId, out var dependency))
                        {
                            Console.WriteLine($"  {dependency.FullName}");
                            Console.WriteLine($"      {dependency.FilePath}:{dependency.LineNumber}");
                            Console.WriteLine();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("  No dependencies found (leaf method)");
                }
            }

            return 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during analysis: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 4;
        }
    }
}
