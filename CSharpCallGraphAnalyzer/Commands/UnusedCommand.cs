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
            description: "Output format (json, console)",
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

            var options = new AnalysisOptions
            {
                SolutionPath = solutionPath,
                OutputFormat = format,
                OutputFile = outputFile,
                ExcludeNamespaces = excludeNamespaces
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

            // Detect entry points
            var entryPointDetector = new EntryPointDetector(options);
            var entryPoints = entryPointDetector.DetectEntryPoints(callGraph);

            // Find unused methods
            var deadCodeAnalyzer = new DeadCodeAnalyzer();
            var unusedMethods = deadCodeAnalyzer.FindUnusedMethods(callGraph, entryPoints);

            // Generate output
            var result = JsonOutput.CreateResult(callGraph, unusedMethods, solutionPath, includeCallGraph: false);

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
