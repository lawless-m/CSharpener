using CSharpCallGraphAnalyzer.McpServer.McpProtocol;

// MCP server uses stdio for communication
Console.Error.WriteLine("Starting CSharpener MCP Server...");

var server = new McpServer();
await server.RunAsync();

Console.Error.WriteLine("CSharpener MCP Server stopped.");
