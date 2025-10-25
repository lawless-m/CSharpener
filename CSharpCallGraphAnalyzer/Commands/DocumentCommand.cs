using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using CSharpCallGraphAnalyzer.Analysis;
using CSharpCallGraphAnalyzer.Configuration;
using CSharpCallGraphAnalyzer.Models;

namespace CSharpCallGraphAnalyzer.Commands;

/// <summary>
/// Command to generate documentation metadata for Claude Code
/// </summary>
public class DocumentCommand
{
    public static Command Create()
    {
        var command = new Command("document", "Generate documentation metadata for Claude Code to use when generating docs");

        var solutionOption = new Option<string>(
            aliases: new[] { "--solution", "-s" },
            description: "Path to solution or project file");
        solutionOption.IsRequired = true;

        var outputOption = new Option<string?>(
            aliases: new[] { "--output", "-o" },
            description: "Output file path (default: stdout)");

        var formatOption = new Option<string>(
            aliases: new[] { "--format", "-f" },
            description: "Output format: json or console",
            getDefaultValue: () => "json");

        var filterOption = new Option<string?>(
            aliases: new[] { "--filter" },
            description: "Filter by namespace or class pattern (e.g., 'MyApp.Business.*')");

        var methodOption = new Option<string?>(
            aliases: new[] { "--method", "-m" },
            description: "Specific method to document (full name)");

        var includeUnusedOption = new Option<bool>(
            aliases: new[] { "--include-unused" },
            description: "Include unused methods in output",
            getDefaultValue: () => false);

        var includeTestsOption = new Option<bool>(
            aliases: new[] { "--include-tests" },
            description: "Include test methods in output",
            getDefaultValue: () => false);

        command.AddOption(solutionOption);
        command.AddOption(outputOption);
        command.AddOption(formatOption);
        command.AddOption(filterOption);
        command.AddOption(methodOption);
        command.AddOption(includeUnusedOption);
        command.AddOption(includeTestsOption);

        command.SetHandler(async (string solution, string? output, string format, string? filter,
            string? method, bool includeUnused, bool includeTests) =>
        {
            var exitCode = await ExecuteAsync(solution, output, format, filter, method, includeUnused, includeTests);
            Environment.Exit(exitCode);
        }, solutionOption, outputOption, formatOption, filterOption, methodOption, includeUnusedOption, includeTestsOption);

        return command;
    }

    private static async Task<int> ExecuteAsync(
        string solutionPath,
        string? outputPath,
        string format,
        string? filter,
        string? methodName,
        bool includeUnused,
        bool includeTests)
    {
        try
        {
            Console.Error.WriteLine($"Loading solution: {solutionPath}");

            // Load configuration
            var options = ConfigurationLoader.LoadConfiguration(solutionPath);
            options.SolutionPath = solutionPath;
            options.OutputFormat = format;
            options.IncludeTests = includeTests;

            // Load solution
            var solutionLoader = new SolutionLoader(options);
            var solution = await solutionLoader.LoadSolutionAsync(solutionPath);
            if (solution == null)
            {
                Console.Error.WriteLine("Failed to load solution");
                return 3;
            }

            // Check cache first
            var cache = new CallGraphCache(options);
            var cachedResult = await cache.TryLoadCacheAsync(solution);

            CallGraph callGraph;
            List<Compilation> compilations;

            if (cachedResult != null)
            {
                Console.Error.WriteLine("Using cached analysis");
                callGraph = cachedResult.CallGraph;
                compilations = cachedResult.Compilations;
            }
            else
            {
                Console.Error.WriteLine("Performing fresh analysis...");

                // Get compilations
                compilations = new List<Compilation>();
                foreach (var project in solution.Projects)
                {
                    var compilation = await project.GetCompilationAsync();
                    if (compilation != null)
                    {
                        compilations.Add(compilation);
                    }
                }

                // Discover methods
                var methodDiscovery = new MethodDiscovery(options);
                var allMethods = await methodDiscovery.DiscoverMethodsAsync(compilations);

                // Build call graph
                var graphBuilder = new CallGraphBuilder(options);
                callGraph = await graphBuilder.BuildCallGraphAsync(allMethods, compilations);

                // Detect entry points
                var entryPointDetector = new EntryPointDetector(options);
                entryPointDetector.DetectEntryPoints(callGraph);

                // Mark used methods
                foreach (var method in callGraph.Methods.Values.Where(m => m.IsEntryPoint))
                {
                    callGraph.MarkAsUsed(method.Id);
                }

                // Save cache for future use
                await cache.SaveCacheAsync(solution, callGraph, compilations);
            }

            // Filter methods
            var methods = FilterMethods(callGraph, filter, methodName, includeUnused, includeTests);

            // Generate output
            if (format == "json")
            {
                var jsonOutput = GenerateJsonOutput(methods, callGraph, solutionPath);
                if (outputPath != null)
                {
                    await File.WriteAllTextAsync(outputPath, jsonOutput);
                    Console.Error.WriteLine($"Documentation metadata written to {outputPath}");
                }
                else
                {
                    Console.WriteLine(jsonOutput);
                }
            }
            else
            {
                GenerateConsoleOutput(methods, callGraph);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"Inner error: {ex.InnerException.Message}");
            }
            return 4;
        }
    }

    private static List<Models.MethodInfo> FilterMethods(
        CallGraph callGraph,
        string? filter,
        string? methodName,
        bool includeUnused,
        bool includeTests)
    {
        var methods = callGraph.Methods.Values.ToList();

        // Filter by specific method
        if (!string.IsNullOrEmpty(methodName))
        {
            methods = methods.Where(m =>
                m.FullName.Contains(methodName, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Filter by namespace/class pattern
        if (!string.IsNullOrEmpty(filter))
        {
            methods = methods.Where(m => MatchesPattern(m, filter)).ToList();
        }

        // Filter unused methods
        if (!includeUnused)
        {
            methods = methods.Where(m => m.IsUsed).ToList();
        }

        // Filter test methods
        if (!includeTests)
        {
            methods = methods.Where(m => !IsTestMethod(m)).ToList();
        }

        return methods;
    }

    private static bool MatchesPattern(Models.MethodInfo method, string pattern)
    {
        if (pattern.Contains('*'))
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                .Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(method.FullName, regex,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return method.FullName.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
               method.Namespace.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
               method.ClassName.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestMethod(Models.MethodInfo method)
    {
        var testAttributes = new[]
        {
            "Test", "TestMethod", "Fact", "Theory", "TestCase",
            "Xunit.FactAttribute", "Xunit.TheoryAttribute",
            "NUnit.Framework.TestAttribute", "NUnit.Framework.TestCaseAttribute",
            "Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute"
        };

        return method.Attributes.Any(attr =>
            testAttributes.Any(test => attr.Contains(test, StringComparison.OrdinalIgnoreCase)));
    }

    private static string GenerateJsonOutput(List<Models.MethodInfo> methods, CallGraph callGraph, string solutionPath)
    {
        var result = new
        {
            version = "1.0",
            generatedAt = DateTime.UtcNow.ToString("O"),
            solution = solutionPath,
            purpose = "Documentation metadata for Claude Code",
            summary = new
            {
                totalMethods = methods.Count,
                methodsWithDocs = methods.Count(m => !string.IsNullOrEmpty(m.ExistingDocumentation)),
                methodsWithoutDocs = methods.Count(m => string.IsNullOrEmpty(m.ExistingDocumentation)),
                publicMethods = methods.Count(m => m.Accessibility == "public"),
                entryPoints = methods.Count(m => m.IsEntryPoint),
                asyncMethods = methods.Count(m => m.IsAsync),
                interfaceImplementations = methods.Count(m => m.ImplementsInterface)
            },
            methods = methods.Select(m => new
            {
                // Basic info
                id = m.Id,
                name = m.Name,
                fullName = m.FullName,
                @namespace = m.Namespace,
                className = m.ClassName,
                signature = m.Signature,

                // Location
                filePath = m.FilePath,
                lineNumber = m.LineNumber,
                linesOfCode = m.LinesOfCode,

                // Method characteristics
                accessibility = m.Accessibility,
                isStatic = m.IsStatic,
                isAsync = m.IsAsync,
                isAbstract = m.IsAbstract,
                isVirtual = m.IsVirtual,
                isOverride = m.IsOverride,

                // Parameters and return type
                parameters = m.Parameters.Select(p => new
                {
                    name = p.Name,
                    type = p.Type,
                    typeDisplayName = p.TypeDisplayName,
                    hasDefaultValue = p.HasDefaultValue,
                    defaultValue = p.DefaultValue,
                    isRef = p.IsRef,
                    isOut = p.IsOut,
                    isIn = p.IsIn,
                    isParams = p.IsParams,
                    isOptional = p.IsOptional,
                    attributes = p.Attributes
                }).ToList(),
                returnType = m.ReturnType,
                returnTypeDisplayName = m.ReturnTypeDisplayName,

                // Generics
                genericParameters = m.GenericParameters,
                genericConstraints = m.GenericConstraints,

                // Attributes
                attributes = m.Attributes,

                // Call graph context
                isUsed = m.IsUsed,
                isEntryPoint = m.IsEntryPoint,
                callerCount = callGraph.GetCallers(m.Id).Count(),
                calleeCount = callGraph.GetCallees(m.Id).Count(),

                // Class context
                classInterfaces = m.ClassInterfaces,
                classBaseType = m.ClassBaseType,
                classIsAbstract = m.ClassIsAbstract,
                classIsSealed = m.ClassIsSealed,
                classIsStatic = m.ClassIsStatic,

                // Interface implementation
                implementsInterface = m.ImplementsInterface,
                implementedInterfaceMethod = m.ImplementedInterfaceMethod,

                // Existing documentation
                hasExistingDocs = !string.IsNullOrEmpty(m.ExistingDocumentation),
                existingDocumentation = m.ExistingDocumentation
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        return JsonSerializer.Serialize(result, options);
    }

    private static void GenerateConsoleOutput(List<Models.MethodInfo> methods, CallGraph callGraph)
    {
        Console.WriteLine($"Documentation Metadata for {methods.Count} methods\n");
        Console.WriteLine("=" .PadRight(80, '='));

        var grouped = methods.GroupBy(m => m.ClassName).OrderBy(g => g.Key);

        foreach (var classGroup in grouped)
        {
            Console.WriteLine($"\nClass: {classGroup.Key}");
            Console.WriteLine("-".PadRight(80, '-'));

            foreach (var method in classGroup.OrderBy(m => m.Name))
            {
                Console.WriteLine($"\n  {method.Signature}");
                Console.WriteLine($"    Location: {method.FilePath}:{method.LineNumber}");
                Console.WriteLine($"    Accessibility: {method.Accessibility}");
                Console.WriteLine($"    Used: {method.IsUsed}, Entry Point: {method.IsEntryPoint}");

                if (method.IsAsync)
                    Console.WriteLine($"    Async: true");

                if (method.Parameters.Any())
                {
                    Console.WriteLine($"    Parameters: {method.Parameters.Count}");
                    foreach (var p in method.Parameters)
                    {
                        var modifiers = new List<string>();
                        if (p.IsRef) modifiers.Add("ref");
                        if (p.IsOut) modifiers.Add("out");
                        if (p.IsIn) modifiers.Add("in");
                        if (p.IsParams) modifiers.Add("params");
                        var prefix = modifiers.Any() ? string.Join(" ", modifiers) + " " : "";
                        var defaultVal = p.HasDefaultValue ? $" = {p.DefaultValue}" : "";
                        Console.WriteLine($"      - {prefix}{p.TypeDisplayName} {p.Name}{defaultVal}");
                    }
                }

                Console.WriteLine($"    Return Type: {method.ReturnTypeDisplayName}");

                if (method.GenericParameters.Any())
                {
                    Console.WriteLine($"    Generic Parameters: <{string.Join(", ", method.GenericParameters)}>");
                    if (method.GenericConstraints.Any())
                    {
                        foreach (var constraint in method.GenericConstraints)
                        {
                            Console.WriteLine($"      {constraint}");
                        }
                    }
                }

                if (method.ImplementsInterface)
                    Console.WriteLine($"    Implements: {method.ImplementedInterfaceMethod}");

                var callers = callGraph.GetCallers(method.Id).Count();
                var callees = callGraph.GetCallees(method.Id).Count();
                Console.WriteLine($"    Call Graph: {callers} caller(s), {callees} callee(s)");

                if (!string.IsNullOrEmpty(method.ExistingDocumentation))
                {
                    Console.WriteLine($"    Has Documentation: Yes");
                }
                else
                {
                    Console.WriteLine($"    Has Documentation: No - needs docs!");
                }
            }
        }

        Console.WriteLine("\n" + "=".PadRight(80, '='));
        Console.WriteLine($"\nSummary:");
        Console.WriteLine($"  Total methods: {methods.Count}");
        Console.WriteLine($"  With documentation: {methods.Count(m => !string.IsNullOrEmpty(m.ExistingDocumentation))}");
        Console.WriteLine($"  Without documentation: {methods.Count(m => string.IsNullOrEmpty(m.ExistingDocumentation))}");
        Console.WriteLine($"  Public methods: {methods.Count(m => m.Accessibility == "public")}");
        Console.WriteLine($"  Entry points: {methods.Count(m => m.IsEntryPoint)}");
    }
}
