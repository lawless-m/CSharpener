using System.CommandLine;
using System.CommandLine.Invocation;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.Output;

namespace CSharpCallGraphAnalyzer.Commands;

/// <summary>
/// Command to find all callers of a specific method
/// </summary>
public class CallersCommand : Command
{
    public CallersCommand() : base("callers", "Find all methods that call a specific method")
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
            Console.Error.WriteLine($"Finding callers of: {methodName}");

            // Create base options from CLI arguments
            var baseOptions = new AnalysisOptions
            {
                SolutionPath = solutionPath,
                OutputFormat = format
            };

            // Load configuration from .csharp-analyzer.json if it exists
            var options = ConfigurationLoader.LoadConfiguration(solutionPath, baseOptions);

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

            // Get callers
            var callers = callGraph.GetCallers(targetMethod.Id).ToList();

            Console.Error.WriteLine($"Found {callers.Count} caller(s)");

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonOutput.CreateCallersResult(callGraph, targetMethod.Id, targetMethod.FullName);
                Console.WriteLine(json);
            }
            else if (format.Equals("dot", StringComparison.OrdinalIgnoreCase) || format.Equals("graphviz", StringComparison.OrdinalIgnoreCase))
            {
                var dot = DotOutput.GenerateCallersDot(callGraph, targetMethod.Id, maxDepth: 3);
                Console.WriteLine(dot);
                Console.Error.WriteLine($"\nGenerate visualization with: dot -Tpng -o callers.png");
            }
            else
            {
                Console.WriteLine($"\nCallers of: {targetMethod.FullName}");
                Console.WriteLine($"Location: {targetMethod.FilePath}:{targetMethod.LineNumber}");
                Console.WriteLine($"\nTotal callers: {callers.Count}");
                Console.WriteLine();

                if (callers.Count > 0)
                {
                    foreach (var callerId in callers)
                    {
                        if (callGraph.Methods.TryGetValue(callerId, out var caller))
                        {
                            Console.WriteLine($"  {caller.FullName}");
                            Console.WriteLine($"      {caller.FilePath}:{caller.LineNumber}");
                            Console.WriteLine();
                        }
                    }
                }
                else
                {
                    Console.WriteLine("  No callers found");
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
