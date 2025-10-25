using System.CommandLine;
using System.CommandLine.Invocation;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.Output;

namespace CSharpCallGraphAnalyzer.Commands;

/// <summary>
/// Command to find unused methods in a solution
/// </summary>
public class UnusedCommand : Command
{
    public UnusedCommand() : base("unused", "Find unused methods in the solution")
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

        var excludeNamespaceOption = new Option<string[]>(
            aliases: new[] { "--exclude-namespace" },
            description: "Namespaces to exclude from analysis",
            getDefaultValue: () => Array.Empty<string>());

        AddOption(solutionOption);
        AddOption(formatOption);
        AddOption(outputOption);
        AddOption(excludeNamespaceOption);

        this.SetHandler(async (context) =>
        {
            var solution = context.ParseResult.GetValueForOption(solutionOption)!;
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var output = context.ParseResult.GetValueForOption(outputOption);
            var excludeNamespaces = context.ParseResult.GetValueForOption(excludeNamespaceOption)!;

            var exitCode = await ExecuteAsync(solution, format, output, excludeNamespaces, context.GetCancellationToken());
            context.ExitCode = exitCode;
        });
    }

    private async Task<int> ExecuteAsync(
        string solutionPath,
        string format,
        string? outputFile,
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

            // Try to load from cache
            var cache = new CallGraphCache(options);
            var cached = await cache.TryLoadCacheAsync(solution);

            CallGraph callGraph;
            List<string> entryPoints;
            List<AnalysisWarning> warnings = new();

            if (cached != null)
            {
                // Use cached call graph
                callGraph = cached.CallGraph;
                entryPoints = cached.EntryPoints;
                Console.Error.WriteLine($"Using cached analysis with {callGraph.Methods.Count} methods");
            }
            else
            {
                // Perform full analysis
                Console.Error.WriteLine("Performing full analysis...");

                // Discover methods
                var discovery = new MethodDiscovery(options);
                var methods = await discovery.DiscoverMethodsAsync(compilations, cancellationToken);

                // Build call graph
                var graphBuilder = new CallGraphBuilder();
                callGraph = await graphBuilder.BuildCallGraphAsync(methods, compilations, cancellationToken);

                // Detect entry points
                var entryPointDetector = new EntryPointDetector(options);
                entryPoints = entryPointDetector.DetectEntryPoints(callGraph);

                // Run reflection analyzer if enabled
                if (options.DetectReflection)
                {
                    var reflectionAnalyzer = new ReflectionAnalyzer();
                    var reflectionWarnings = reflectionAnalyzer.DetectReflectionUsage(compilations, callGraph);
                    warnings.AddRange(reflectionWarnings);
                }

                // Run DI analyzer if enabled
                if (options.DetectDependencyInjection)
                {
                    var diAnalyzer = new DependencyInjectionAnalyzer();
                    var diWarnings = diAnalyzer.DetectDependencyInjectionUsage(compilations, callGraph);
                    warnings.AddRange(diWarnings);

                    // Re-detect entry points after DI analysis
                    entryPoints = callGraph.Methods.Values.Where(m => m.IsEntryPoint).Select(m => m.Id).ToList();
                }

                // Save to cache
                await cache.SaveCacheAsync(solution, callGraph, entryPoints);
            }

            // Find unused methods
            var deadCodeAnalyzer = new DeadCodeAnalyzer();
            var unusedMethods = deadCodeAnalyzer.FindUnusedMethods(callGraph, entryPoints);

            // Generate output
            var result = JsonOutput.CreateResult(callGraph, unusedMethods, solutionPath, includeCallGraph: false);
            result.Warnings = warnings;

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
                // GraphViz DOT format - show only unused methods grouped by class
                var dotOptions = new DotOutputOptions
                {
                    IncludeLegend = true,
                    MinConfidence = ConfidenceLevel.Low
                };
                var dot = DotOutput.GenerateUnusedMethodsDot(callGraph, unusedMethods, dotOptions);

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
                Console.WriteLine($"\nAnalysis Results:");
                Console.WriteLine($"Total Methods: {result.Summary.TotalMethods}");
                Console.WriteLine($"Used Methods: {result.Summary.UsedMethods}");
                Console.WriteLine($"Unused Methods: {result.Summary.UnusedMethods}");
                Console.WriteLine();

                if (result.UnusedMethods.Count > 0)
                {
                    Console.WriteLine("Unused Methods:");
                    foreach (var method in result.UnusedMethods)
                    {
                        Console.WriteLine($"  [{method.Confidence}] {method.FullName}");
                        Console.WriteLine($"      {method.FilePath}:{method.LineNumber}");
                        Console.WriteLine($"      Reason: {method.Reason}");
                        Console.WriteLine();
                    }
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
