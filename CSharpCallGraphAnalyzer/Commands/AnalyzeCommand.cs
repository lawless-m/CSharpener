using System.CommandLine;
using System.CommandLine.Invocation;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.Output;

namespace CSharpCallGraphAnalyzer.Commands;

/// <summary>
/// Command to perform full analysis including call graph
/// </summary>
public class AnalyzeCommand : Command
{
    public AnalyzeCommand() : base("analyze", "Perform full analysis of the solution including call graph")
    {
        var solutionOption = new Option<string>(
            aliases: new[] { "--solution", "-s" },
            description: "Path to the solution or project file")
        {
            IsRequired = true
        };

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            description: "Output format (json, console, dot, graphviz)",
            getDefaultValue: () => "json");

        var outputOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path (if not specified, outputs to stdout)");

        var includeCallGraphOption = new Option<bool>(
            aliases: new[] { "--include-call-graph" },
            description: "Include full call graph in output",
            getDefaultValue: () => true);

        var excludeNamespaceOption = new Option<string[]>(
            aliases: new[] { "--exclude-namespace" },
            description: "Namespaces to exclude from analysis",
            getDefaultValue: () => Array.Empty<string>());

        AddOption(solutionOption);
        AddOption(formatOption);
        AddOption(outputOption);
        AddOption(includeCallGraphOption);
        AddOption(excludeNamespaceOption);

        this.SetHandler(async (context) =>
        {
            var solution = context.ParseResult.GetValueForOption(solutionOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption);
            var includeCallGraph = context.ParseResult.GetValueForOption(includeCallGraphOption);
            var excludeNamespaces = context.ParseResult.GetValueForOption(excludeNamespaceOption)!;

            var exitCode = await ExecuteAsync(solution, format, output, includeCallGraph, excludeNamespaces, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
    }

    private async Task<int> ExecuteAsync(
        string solutionPath,
        string format,
        string? outputFile,
        bool includeCallGraph,
        string[] excludeNamespaces,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.Error.WriteLine($"Analyzing solution: {solutionPath}");

            // Create base options from CLI arguments
            var baseOptions = new AnalysisOptions
            {
                SolutionPath = solutionPath,
                OutputFormat = format,
                OutputFile = outputFile,
                IncludeCallGraph = includeCallGraph,
                ExcludeNamespaces = excludeNamespaces
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

            // Detect entry points
            var entryPointDetector = new EntryPointDetector(options);
            var entryPoints = entryPointDetector.DetectEntryPoints(callGraph);

            // Find unused methods
            var deadCodeAnalyzer = new DeadCodeAnalyzer();
            var unusedMethods = deadCodeAnalyzer.FindUnusedMethods(callGraph, entryPoints);

            // Generate output
            var result = JsonOutput.CreateResult(callGraph, unusedMethods, solutionPath, includeCallGraph);

            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonOutput.ToJson(result);

                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, json, cancellationToken);
                    Console.Error.WriteLine($"Results written to: {outputFile}");
                }
                else
                {
                    Console.WriteLine(json);
                }
            }
            else if (format.Equals("dot", StringComparison.OrdinalIgnoreCase) || format.Equals("graphviz", StringComparison.OrdinalIgnoreCase))
            {
                // GraphViz DOT format
                var dotOptions = new DotOutputOptions
                {
                    IncludeLegend = true,
                    IncludeClassName = true
                };
                var dot = DotOutput.GenerateDot(callGraph, result, dotOptions);

                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, dot, cancellationToken);
                    Console.Error.WriteLine($"DOT file written to: {outputFile}");
                    Console.Error.WriteLine($"Generate visualization with: dot -Tpng {outputFile} -o output.png");
                }
                else
                {
                    Console.WriteLine(dot);
                }
            }
            else
            {
                // Console format
                Console.WriteLine($"\n=== Call Graph Analysis Results ===");
                Console.WriteLine($"\nSolution: {solutionPath}");
                Console.WriteLine($"Analyzed at: {result.AnalyzedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine();
                Console.WriteLine($"Summary:");
                Console.WriteLine($"  Total Methods: {result.Summary.TotalMethods}");
                Console.WriteLine($"  Used Methods: {result.Summary.UsedMethods}");
                Console.WriteLine($"  Unused Methods: {result.Summary.UnusedMethods}");
                Console.WriteLine($"  Entry Points: {entryPoints.Count}");
                Console.WriteLine();

                if (result.UnusedMethods.Count > 0)
                {
                    Console.WriteLine("Unused Methods (sorted by confidence):");
                    var sortedUnused = result.UnusedMethods
                        .OrderByDescending(m => m.Confidence)
                        .ToList();

                    foreach (var method in sortedUnused)
                    {
                        Console.WriteLine($"\n  [{method.Confidence.ToUpper()}] {method.FullName}");
                        Console.WriteLine($"      Location: {method.FilePath}:{method.LineNumber}");
                        Console.WriteLine($"      Accessibility: {method.Accessibility}");
                        Console.WriteLine($"      Reason: {method.Reason}");
                    }
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine("No unused methods detected!");
                }

                if (includeCallGraph && result.CallGraph != null)
                {
                    Console.WriteLine($"\nCall Graph Statistics:");
                    var totalCalls = result.CallGraph.Methods.Sum(m => m.Value.Calls.Count);
                    Console.WriteLine($"  Total method calls: {totalCalls}");
                    Console.WriteLine($"  Average calls per method: {(double)totalCalls / result.CallGraph.Methods.Count:F2}");
                }
            }

            // Return 1 if there are unused methods (warning), 0 otherwise
            return result.UnusedMethods.Count > 0 ? 1 : 0;
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
