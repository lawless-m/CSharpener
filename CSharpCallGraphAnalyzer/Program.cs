using System.CommandLine;
using CSharpCallGraphAnalyzer.Commands;

namespace CSharpCallGraphAnalyzer;

/// <summary>
/// Entry point for the C# Call Graph Analyzer CLI
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("C# Call Graph Analyzer - Analyze C# codebases for unused code, call graphs, and dependencies");

        // Add commands
        rootCommand.AddCommand(new AnalyzeCommand());
        rootCommand.AddCommand(new UnusedCommand());
        rootCommand.AddCommand(new CallersCommand());
        rootCommand.AddCommand(new DependenciesCommand());
        rootCommand.AddCommand(new ImpactCommand());
        rootCommand.AddCommand(DocumentCommand.Create());

        // Parse and execute
        return await rootCommand.InvokeAsync(args);
    }
}
