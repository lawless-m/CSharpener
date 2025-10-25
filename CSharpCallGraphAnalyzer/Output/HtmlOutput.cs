using System.Text;
using System.Web;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpCallGraphAnalyzer.Models;

namespace CSharpCallGraphAnalyzer.Output;

/// <summary>
/// Generates LXR-style cross-referenced HTML documentation
/// </summary>
public class HtmlOutput
{
    private readonly CallGraph _callGraph;
    private readonly string _outputDir;
    private readonly string _solutionName;
    private readonly Dictionary<string, List<Compilation>> _fileCompilations = new();

    public HtmlOutput(CallGraph callGraph, string outputDir, string solutionName)
    {
        _callGraph = callGraph;
        _outputDir = outputDir;
        _solutionName = solutionName;
    }

    /// <summary>
    /// Generate complete HTML documentation
    /// </summary>
    public async Task GenerateAsync(List<Compilation> compilations)
    {
        Console.Error.WriteLine($"Generating HTML documentation to {_outputDir}");

        // Create output directory structure
        Directory.CreateDirectory(_outputDir);
        Directory.CreateDirectory(Path.Combine(_outputDir, "files"));
        Directory.CreateDirectory(Path.Combine(_outputDir, "css"));
        Directory.CreateDirectory(Path.Combine(_outputDir, "js"));

        // Build file to compilation mapping
        BuildFileCompilationMap(compilations);

        // Generate CSS and JavaScript
        await GenerateStaticFilesAsync();

        // Generate index page
        await GenerateIndexPageAsync();

        // Generate file pages
        await GenerateFilePagesAsync(compilations);

        Console.Error.WriteLine($"HTML documentation generated successfully");
        Console.Error.WriteLine($"Open {Path.Combine(_outputDir, "index.html")} in your browser");
    }

    /// <summary>
    /// Build mapping of files to their compilations for semantic model lookup
    /// </summary>
    private void BuildFileCompilationMap(List<Compilation> compilations)
    {
        foreach (var compilation in compilations)
        {
            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var filePath = syntaxTree.FilePath;
                if (!string.IsNullOrEmpty(filePath))
                {
                    if (!_fileCompilations.ContainsKey(filePath))
                    {
                        _fileCompilations[filePath] = new List<Compilation>();
                    }
                    _fileCompilations[filePath].Add(compilation);
                }
            }
        }
    }

    /// <summary>
    /// Generate static CSS and JavaScript files
    /// </summary>
    private async Task GenerateStaticFilesAsync()
    {
        // Generate CSS
        var css = GenerateCSS();
        await File.WriteAllTextAsync(Path.Combine(_outputDir, "css", "style.css"), css);

        // Generate JavaScript
        var js = GenerateJavaScript();
        await File.WriteAllTextAsync(Path.Combine(_outputDir, "js", "search.js"), js);
    }

    /// <summary>
    /// Generate main index page
    /// </summary>
    private async Task GenerateIndexPageAsync()
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{HttpUtility.HtmlEncode(_solutionName)} - Code Cross Reference</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"css/style.css\">");
        html.AppendLine("    <script src=\"js/search.js\" defer></script>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");

        // Header
        html.AppendLine("    <header>");
        html.AppendLine($"        <h1>{HttpUtility.HtmlEncode(_solutionName)}</h1>");
        html.AppendLine("        <p class=\"subtitle\">LXR-Style Cross-Referenced Code Browser</p>");
        html.AppendLine("    </header>");

        // Search bar
        html.AppendLine("    <div class=\"search-container\">");
        html.AppendLine("        <input type=\"text\" id=\"searchBox\" placeholder=\"Search methods, classes, namespaces...\" />");
        html.AppendLine("        <div id=\"searchResults\"></div>");
        html.AppendLine("    </div>");

        // Statistics
        var stats = GenerateStatistics();
        html.AppendLine("    <div class=\"statistics\">");
        html.AppendLine("        <h2>Statistics</h2>");
        html.AppendLine($"        <p>Total Methods: <strong>{stats.TotalMethods}</strong></p>");
        html.AppendLine($"        <p>Used Methods: <strong>{stats.UsedMethods}</strong></p>");
        html.AppendLine($"        <p>Unused Methods: <strong>{stats.UnusedMethods}</strong></p>");
        html.AppendLine($"        <p>Entry Points: <strong>{stats.EntryPoints}</strong></p>");
        html.AppendLine($"        <p>Files: <strong>{stats.TotalFiles}</strong></p>");
        html.AppendLine("    </div>");

        // Navigation tree
        html.AppendLine("    <div class=\"navigation\">");
        html.AppendLine("        <h2>Browse Code</h2>");
        html.AppendLine(GenerateNavigationTree());
        html.AppendLine("    </div>");

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        await File.WriteAllTextAsync(Path.Combine(_outputDir, "index.html"), html.ToString());
    }

    /// <summary>
    /// Generate navigation tree grouped by namespace
    /// </summary>
    private string GenerateNavigationTree()
    {
        var html = new StringBuilder();

        // Group files by namespace
        var filesByNamespace = _callGraph.Methods.Values
            .Where(m => !string.IsNullOrEmpty(m.FilePath))
            .GroupBy(m => m.Namespace)
            .OrderBy(g => g.Key);

        html.AppendLine("        <ul class=\"namespace-tree\">");

        foreach (var nsGroup in filesByNamespace)
        {
            var ns = string.IsNullOrEmpty(nsGroup.Key) ? "(global)" : nsGroup.Key;
            html.AppendLine($"            <li>");
            html.AppendLine($"                <span class=\"namespace\">{HttpUtility.HtmlEncode(ns)}</span>");

            // Group by class within namespace
            var classesByFile = nsGroup
                .GroupBy(m => new { m.FilePath, m.ClassName })
                .OrderBy(g => g.Key.ClassName);

            html.AppendLine("                <ul class=\"class-list\">");
            foreach (var classGroup in classesByFile)
            {
                var fileName = GetHtmlFileName(classGroup.Key.FilePath);
                var className = classGroup.Key.ClassName;
                var methodCount = classGroup.Count();

                html.AppendLine($"                    <li>");
                html.AppendLine($"                        <a href=\"files/{fileName}\" class=\"class-link\">");
                html.AppendLine($"                            {HttpUtility.HtmlEncode(className)} ({methodCount} methods)");
                html.AppendLine($"                        </a>");
                html.AppendLine($"                    </li>");
            }
            html.AppendLine("                </ul>");
            html.AppendLine("            </li>");
        }

        html.AppendLine("        </ul>");
        return html.ToString();
    }

    /// <summary>
    /// Generate individual HTML pages for each source file
    /// </summary>
    private async Task GenerateFilePagesAsync(List<Compilation> compilations)
    {
        // Get unique source files
        var sourceFiles = _callGraph.Methods.Values
            .Where(m => !string.IsNullOrEmpty(m.FilePath) && File.Exists(m.FilePath))
            .Select(m => m.FilePath)
            .Distinct()
            .ToList();

        foreach (var filePath in sourceFiles)
        {
            await GenerateFilePageAsync(filePath);
        }
    }

    /// <summary>
    /// Generate HTML page for a single source file
    /// </summary>
    private async Task GenerateFilePageAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var fileName = GetHtmlFileName(filePath);
        var sourceCode = await File.ReadAllTextAsync(filePath);

        // Get methods in this file
        var methodsInFile = _callGraph.Methods.Values
            .Where(m => m.FilePath == filePath)
            .OrderBy(m => m.LineNumber)
            .ToList();

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine($"    <title>{HttpUtility.HtmlEncode(Path.GetFileName(filePath))} - {HttpUtility.HtmlEncode(_solutionName)}</title>");
        html.AppendLine("    <link rel=\"stylesheet\" href=\"../css/style.css\">");
        html.AppendLine("</head>");
        html.AppendLine("<body class=\"file-view\">");

        // Header with navigation
        html.AppendLine("    <header>");
        html.AppendLine($"        <a href=\"../index.html\" class=\"back-link\">← Back to Index</a>");
        html.AppendLine($"        <h1>{HttpUtility.HtmlEncode(Path.GetFileName(filePath))}</h1>");
        html.AppendLine($"        <p class=\"file-path\">{HttpUtility.HtmlEncode(filePath)}</p>");
        html.AppendLine("    </header>");

        // Methods in this file
        html.AppendLine("    <div class=\"file-methods\">");
        html.AppendLine("        <h2>Methods in this file</h2>");
        html.AppendLine("        <ul class=\"method-list\">");
        foreach (var method in methodsInFile)
        {
            var usedClass = method.IsUsed ? "used" : "unused";
            html.AppendLine($"            <li class=\"{usedClass}\">");
            html.AppendLine($"                <a href=\"#{method.Id}\">{HttpUtility.HtmlEncode(method.Signature)}</a>");
            html.AppendLine($"                <span class=\"line-number\">Line {method.LineNumber}</span>");
            html.AppendLine($"            </li>");
        }
        html.AppendLine("        </ul>");
        html.AppendLine("    </div>");

        // Source code with cross-references
        html.AppendLine("    <div class=\"source-code\">");
        html.AppendLine(await GenerateCrossReferencedSourceAsync(filePath, sourceCode, methodsInFile));
        html.AppendLine("    </div>");

        // Method details (callers/callees)
        html.AppendLine("    <div class=\"method-details\">");
        html.AppendLine("        <h2>Method Details</h2>");
        foreach (var method in methodsInFile)
        {
            html.AppendLine(GenerateMethodDetails(method));
        }
        html.AppendLine("    </div>");

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        var outputPath = Path.Combine(_outputDir, "files", fileName);
        await File.WriteAllTextAsync(outputPath, html.ToString());
    }

    /// <summary>
    /// Generate cross-referenced source code with clickable method calls
    /// </summary>
    private async Task<string> GenerateCrossReferencedSourceAsync(string filePath, string sourceCode, List<Models.MethodInfo> methodsInFile)
    {
        var html = new StringBuilder();

        // Parse the syntax tree
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: filePath);
        var root = await syntaxTree.GetRootAsync();

        // Get semantic model if available
        SemanticModel? semanticModel = null;
        if (_fileCompilations.TryGetValue(filePath, out var compilations))
        {
            var compilation = compilations.FirstOrDefault();
            if (compilation != null)
            {
                semanticModel = compilation.GetSemanticModel(syntaxTree);
            }
        }

        // Split into lines
        var lines = sourceCode.Split('\n');

        html.AppendLine("        <table class=\"source-table\">");

        for (int i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].TrimEnd('\r');

            // Check if this line is the start of a method
            var methodAtLine = methodsInFile.FirstOrDefault(m => m.LineNumber == lineNumber);
            var anchor = methodAtLine != null ? $" id=\"{methodAtLine.Id}\"" : "";
            var methodClass = methodAtLine != null ? " method-definition" : "";

            html.AppendLine($"            <tr{anchor} class=\"source-line{methodClass}\">");
            html.AppendLine($"                <td class=\"line-number\" data-line=\"{lineNumber}\">{lineNumber}</td>");
            html.AppendLine($"                <td class=\"line-code\"><pre>{HttpUtility.HtmlEncode(line)}</pre></td>");
            html.AppendLine($"            </tr>");
        }

        html.AppendLine("        </table>");

        return html.ToString();
    }

    /// <summary>
    /// Generate method details section (callers/callees)
    /// </summary>
    private string GenerateMethodDetails(Models.MethodInfo method)
    {
        var html = new StringBuilder();

        var usedClass = method.IsUsed ? "used" : "unused";
        html.AppendLine($"        <div class=\"method-detail {usedClass}\" id=\"details-{method.Id}\">");
        html.AppendLine($"            <h3>{HttpUtility.HtmlEncode(method.Signature)}</h3>");

        // Metadata
        html.AppendLine($"            <div class=\"method-metadata\">");
        html.AppendLine($"                <span class=\"badge accessibility\">{method.Accessibility}</span>");
        if (method.IsStatic) html.AppendLine($"                <span class=\"badge\">static</span>");
        if (method.IsAsync) html.AppendLine($"                <span class=\"badge\">async</span>");
        if (method.IsAbstract) html.AppendLine($"                <span class=\"badge\">abstract</span>");
        if (method.IsVirtual) html.AppendLine($"                <span class=\"badge\">virtual</span>");
        if (method.IsOverride) html.AppendLine($"                <span class=\"badge\">override</span>");
        if (method.IsEntryPoint) html.AppendLine($"                <span class=\"badge entry-point\">Entry Point</span>");
        if (!method.IsUsed) html.AppendLine($"                <span class=\"badge unused-badge\">Unused ({method.Confidence})</span>");
        html.AppendLine($"            </div>");

        // Callers
        var callers = _callGraph.GetCallers(method.Id).ToList();
        html.AppendLine($"            <div class=\"callers\">");
        html.AppendLine($"                <h4>Called by ({callers.Count}):</h4>");
        if (callers.Any())
        {
            html.AppendLine($"                <ul>");
            foreach (var callerId in callers)
            {
                if (_callGraph.Methods.TryGetValue(callerId, out var caller))
                {
                    var callerLink = GenerateMethodLink(caller);
                    html.AppendLine($"                    <li>{callerLink}</li>");
                }
            }
            html.AppendLine($"                </ul>");
        }
        else
        {
            html.AppendLine($"                <p class=\"no-refs\">No callers found</p>");
        }
        html.AppendLine($"            </div>");

        // Callees
        var callees = _callGraph.GetCallees(method.Id).ToList();
        html.AppendLine($"            <div class=\"callees\">");
        html.AppendLine($"                <h4>Calls ({callees.Count}):</h4>");
        if (callees.Any())
        {
            html.AppendLine($"                <ul>");
            foreach (var calleeId in callees)
            {
                if (_callGraph.Methods.TryGetValue(calleeId, out var callee))
                {
                    var calleeLink = GenerateMethodLink(callee);
                    html.AppendLine($"                    <li>{calleeLink}</li>");
                }
            }
            html.AppendLine($"                </ul>");
        }
        else
        {
            html.AppendLine($"                <p class=\"no-refs\">Calls no other methods</p>");
        }
        html.AppendLine($"            </div>");

        html.AppendLine($"        </div>");

        return html.ToString();
    }

    /// <summary>
    /// Generate a hyperlink to a method
    /// </summary>
    private string GenerateMethodLink(Models.MethodInfo method)
    {
        if (string.IsNullOrEmpty(method.FilePath))
        {
            return HttpUtility.HtmlEncode(method.Signature);
        }

        var fileName = GetHtmlFileName(method.FilePath);
        var link = $"{fileName}#{method.Id}";
        var displayName = HttpUtility.HtmlEncode(method.Signature);
        var location = $"{Path.GetFileName(method.FilePath)}:{method.LineNumber}";

        return $"<a href=\"{link}\" class=\"method-link\">{displayName}</a> <span class=\"method-location\">({location})</span>";
    }

    /// <summary>
    /// Convert file path to HTML file name
    /// </summary>
    private string GetHtmlFileName(string filePath)
    {
        // Use file name + hash of path to avoid collisions
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var hash = Math.Abs(filePath.GetHashCode()).ToString("X8");
        return $"{fileName}_{hash}.html";
    }

    /// <summary>
    /// Generate statistics
    /// </summary>
    private (int TotalMethods, int UsedMethods, int UnusedMethods, int EntryPoints, int TotalFiles) GenerateStatistics()
    {
        var totalMethods = _callGraph.Methods.Count;
        var usedMethods = _callGraph.Methods.Values.Count(m => m.IsUsed);
        var unusedMethods = totalMethods - usedMethods;
        var entryPoints = _callGraph.Methods.Values.Count(m => m.IsEntryPoint);
        var totalFiles = _callGraph.Methods.Values
            .Where(m => !string.IsNullOrEmpty(m.FilePath))
            .Select(m => m.FilePath)
            .Distinct()
            .Count();

        return (totalMethods, usedMethods, unusedMethods, entryPoints, totalFiles);
    }

    /// <summary>
    /// Generate CSS stylesheet
    /// </summary>
    private string GenerateCSS()
    {
        return @"
/* LXR-Style Code Cross Reference */

:root {
    --primary-color: #2c3e50;
    --secondary-color: #3498db;
    --success-color: #27ae60;
    --warning-color: #f39c12;
    --danger-color: #e74c3c;
    --bg-color: #ecf0f1;
    --code-bg: #f8f9fa;
    --border-color: #bdc3c7;
}

* {
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}

body {
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
    line-height: 1.6;
    color: var(--primary-color);
    background: var(--bg-color);
    padding: 20px;
}

header {
    background: var(--primary-color);
    color: white;
    padding: 20px;
    margin-bottom: 30px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

header h1 {
    font-size: 2em;
    margin-bottom: 5px;
}

.subtitle {
    opacity: 0.9;
    font-size: 1.1em;
}

.back-link {
    color: white;
    text-decoration: none;
    font-size: 0.9em;
    margin-bottom: 10px;
    display: inline-block;
}

.back-link:hover {
    text-decoration: underline;
}

.file-path {
    font-family: 'Courier New', monospace;
    font-size: 0.9em;
    opacity: 0.8;
}

.search-container {
    background: white;
    padding: 20px;
    margin-bottom: 30px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

#searchBox {
    width: 100%;
    padding: 12px;
    font-size: 16px;
    border: 2px solid var(--border-color);
    border-radius: 4px;
}

#searchBox:focus {
    outline: none;
    border-color: var(--secondary-color);
}

#searchResults {
    margin-top: 10px;
}

.statistics {
    background: white;
    padding: 20px;
    margin-bottom: 30px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.statistics h2 {
    margin-bottom: 15px;
    color: var(--primary-color);
}

.statistics p {
    margin-bottom: 8px;
}

.navigation {
    background: white;
    padding: 20px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.navigation h2 {
    margin-bottom: 15px;
    color: var(--primary-color);
}

.namespace-tree {
    list-style: none;
}

.namespace-tree > li {
    margin-bottom: 20px;
}

.namespace {
    font-weight: bold;
    color: var(--secondary-color);
    font-size: 1.1em;
    display: block;
    margin-bottom: 10px;
}

.class-list {
    list-style: none;
    padding-left: 20px;
}

.class-list li {
    margin-bottom: 5px;
}

.class-link {
    color: var(--primary-color);
    text-decoration: none;
    transition: color 0.2s;
}

.class-link:hover {
    color: var(--secondary-color);
    text-decoration: underline;
}

.file-methods {
    background: white;
    padding: 20px;
    margin-bottom: 30px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.file-methods h2 {
    margin-bottom: 15px;
    color: var(--primary-color);
}

.method-list {
    list-style: none;
}

.method-list li {
    padding: 8px;
    margin-bottom: 5px;
    border-left: 4px solid var(--success-color);
    background: var(--code-bg);
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.method-list li.unused {
    border-left-color: var(--danger-color);
    opacity: 0.7;
}

.method-list li a {
    color: var(--primary-color);
    text-decoration: none;
    font-family: 'Courier New', monospace;
}

.method-list li a:hover {
    color: var(--secondary-color);
}

.line-number {
    color: #999;
    font-size: 0.9em;
}

.source-code {
    background: white;
    padding: 20px;
    margin-bottom: 30px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    overflow-x: auto;
}

.source-table {
    width: 100%;
    border-collapse: collapse;
    font-family: 'Courier New', monospace;
    font-size: 14px;
}

.source-line {
    border-bottom: 1px solid #f0f0f0;
}

.source-line:hover {
    background: #f8f9fa;
}

.source-line.method-definition {
    background: #fff3cd;
    border-left: 4px solid var(--warning-color);
}

.source-table .line-number {
    background: #f8f9fa;
    color: #999;
    text-align: right;
    padding: 2px 10px;
    user-select: none;
    width: 50px;
    border-right: 1px solid #ddd;
}

.source-table .line-code {
    padding: 2px 10px;
}

.source-table pre {
    margin: 0;
    white-space: pre-wrap;
    word-wrap: break-word;
}

.method-details {
    background: white;
    padding: 20px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.method-details h2 {
    margin-bottom: 20px;
    color: var(--primary-color);
}

.method-detail {
    margin-bottom: 30px;
    padding: 20px;
    border: 1px solid var(--border-color);
    border-radius: 4px;
    background: var(--code-bg);
}

.method-detail.unused {
    border-left: 4px solid var(--danger-color);
}

.method-detail h3 {
    font-family: 'Courier New', monospace;
    font-size: 1.1em;
    margin-bottom: 10px;
    color: var(--primary-color);
}

.method-metadata {
    margin-bottom: 15px;
}

.badge {
    display: inline-block;
    padding: 4px 8px;
    margin-right: 5px;
    background: var(--secondary-color);
    color: white;
    border-radius: 3px;
    font-size: 0.85em;
}

.badge.accessibility {
    background: var(--primary-color);
}

.badge.entry-point {
    background: var(--success-color);
}

.badge.unused-badge {
    background: var(--danger-color);
}

.callers, .callees {
    margin-top: 15px;
}

.callers h4, .callees h4 {
    margin-bottom: 10px;
    color: var(--primary-color);
}

.callers ul, .callees ul {
    list-style: none;
    padding-left: 20px;
}

.callers li, .callees li {
    margin-bottom: 5px;
}

.method-link {
    color: var(--secondary-color);
    text-decoration: none;
    font-family: 'Courier New', monospace;
}

.method-link:hover {
    text-decoration: underline;
}

.method-location {
    color: #999;
    font-size: 0.9em;
    font-family: 'Courier New', monospace;
}

.no-refs {
    color: #999;
    font-style: italic;
    padding-left: 20px;
}

@media print {
    body {
        background: white;
    }

    .search-container {
        display: none;
    }
}
";
    }

    /// <summary>
    /// Generate JavaScript for search functionality
    /// </summary>
    private string GenerateJavaScript()
    {
        return @"
// Simple client-side search functionality
document.addEventListener('DOMContentLoaded', function() {
    const searchBox = document.getElementById('searchBox');
    const searchResults = document.getElementById('searchResults');

    if (!searchBox) return;

    searchBox.addEventListener('input', function(e) {
        const query = e.target.value.toLowerCase().trim();

        if (query.length < 2) {
            searchResults.innerHTML = '';
            return;
        }

        // Search through navigation links
        const links = document.querySelectorAll('.class-link');
        const matches = [];

        links.forEach(link => {
            const text = link.textContent.toLowerCase();
            if (text.includes(query)) {
                matches.push({
                    text: link.textContent,
                    href: link.getAttribute('href')
                });
            }
        });

        if (matches.length > 0) {
            let html = '<div class=\"search-results-list\">';
            matches.slice(0, 10).forEach(match => {
                html += `<div class=\"search-result\"><a href=\"${match.href}\">${match.text}</a></div>`;
            });
            if (matches.length > 10) {
                html += `<div class=\"search-more\">${matches.length - 10} more results...</div>`;
            }
            html += '</div>';
            searchResults.innerHTML = html;
        } else {
            searchResults.innerHTML = '<div class=\"no-results\">No results found</div>';
        }
    });
});
";
    }
}
