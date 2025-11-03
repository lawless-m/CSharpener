# CSharpener MCP Server - Deployment Guide

This guide explains how to deploy the CSharpener MCP server to use with Claude Desktop.

## Quick Start (Recommended)

### Windows

1. Run the deployment script:
```powershell
.\deploy-mcp.ps1
```

2. Copy the configuration output and add it to:
```
%APPDATA%\Claude\claude_desktop_config.json
```

3. Restart Claude Desktop

### Linux/macOS

1. Make the script executable and run it:
```bash
chmod +x deploy-mcp.sh
./deploy-mcp.sh
```

2. Copy the configuration output and add it to:
```
# macOS
~/Library/Application Support/Claude/claude_desktop_config.json

# Linux
~/.config/Claude/claude_desktop_config.json
```

3. Restart Claude Desktop

## Deployment Options

### Option 1: Development Mode (No Build)

**Pros**: Quick setup, easy debugging, auto-updates with code changes
**Cons**: Slower startup, requires .NET SDK

**Config**:
```json
{
  "mcpServers": {
    "csharpener": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\full\\path\\to\\CSharpCallGraphAnalyzer.McpServer\\CSharpCallGraphAnalyzer.McpServer.csproj"
      ]
    }
  }
}
```

### Option 2: Published Executable (Recommended)

**Pros**: Fast startup, self-contained, no SDK required
**Cons**: Larger file size (~100MB), manual updates

**Build**:
```bash
# Windows
.\deploy-mcp.ps1

# Linux
./deploy-mcp.sh linux-x64

# macOS Intel
./deploy-mcp.sh osx-x64

# macOS Apple Silicon
./deploy-mcp.sh osx-arm64
```

**Config**:
```json
{
  "mcpServers": {
    "csharpener": {
      "command": "C:\\path\\to\\published\\mcp-server\\CSharpCallGraphAnalyzer.McpServer.exe"
    }
  }
}
```

### Option 3: Framework-Dependent (Smallest)

**Pros**: Small file size (~5MB), uses system .NET
**Cons**: Requires .NET 9 runtime installed

**Build**:
```bash
dotnet publish CSharpCallGraphAnalyzer.McpServer/CSharpCallGraphAnalyzer.McpServer.csproj \
    --configuration Release \
    --output ./published/mcp-server-fd \
    /p:PublishSingleFile=true
```

**Config**:
```json
{
  "mcpServers": {
    "csharpener": {
      "command": "dotnet",
      "args": [
        "C:\\path\\to\\published\\mcp-server-fd\\CSharpCallGraphAnalyzer.McpServer.dll"
      ]
    }
  }
}
```

## Verification

### Test the Server Manually

```bash
# Windows
cd published\mcp-server
echo {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}} | .\CSharpCallGraphAnalyzer.McpServer.exe

# Linux/macOS
cd published/mcp-server
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | ./CSharpCallGraphAnalyzer.McpServer
```

Expected output:
```json
{"id":1,"result":{"protocolVersion":"2024-11-05",...}}
```

### Test in Claude Desktop

1. Open Claude Desktop
2. Look for the 🔌 icon in the bottom right
3. Click it to see "csharpener" listed
4. Try asking: "Can you list the available CSharpener tools?"

## Troubleshooting

### Server Not Appearing in Claude

**Check 1**: Config file syntax
```bash
# Validate JSON
python -m json.tool < "%APPDATA%\Claude\claude_desktop_config.json"
```

**Check 2**: Path correctness
- Use absolute paths, not relative
- Use double backslashes on Windows (`\\`)
- Ensure the executable exists and is accessible

**Check 3**: Claude Desktop logs
```
# Windows
%APPDATA%\Claude\logs\

# macOS
~/Library/Logs/Claude/

# Linux
~/.config/Claude/logs/
```

### Server Crashes or Timeouts

**Issue**: Solution analysis taking too long
**Solution**:
- Exclude test projects: `"excludeNamespaces": "*.Tests,*.Test"`
- Analyze smaller projects first
- Use caching (automatic after first run)

**Issue**: "Method not found" errors
**Solution**: Rebuild and republish:
```bash
dotnet clean
.\deploy-mcp.ps1
```

### Permission Errors

**Linux/macOS**: Make executable
```bash
chmod +x published/mcp-server/CSharpCallGraphAnalyzer.McpServer
```

**Windows**: Unblock file
```powershell
Unblock-File published\mcp-server\CSharpCallGraphAnalyzer.McpServer.exe
```

## Distribution

### For Team Members

**Option A**: Share the published folder
1. Run `deploy-mcp.ps1` or `deploy-mcp.sh`
2. Zip the `published/mcp-server` folder
3. Share with team
4. Each person updates their Claude config with the path

**Option B**: Package as installer
1. Use tools like Inno Setup (Windows) or create .deb/.rpm (Linux)
2. Install to standard location (e.g., `C:\Program Files\CSharpener`)
3. Provide config snippet

### For Public Release

1. Create GitHub Release
2. Attach platform-specific executables:
   - `csharpener-mcp-win-x64.zip`
   - `csharpener-mcp-linux-x64.tar.gz`
   - `csharpener-mcp-osx-x64.tar.gz`
   - `csharpener-mcp-osx-arm64.tar.gz`
3. Include config instructions in release notes

## Configuration Examples

### Basic Setup
```json
{
  "mcpServers": {
    "csharpener": {
      "command": "C:\\tools\\csharpener\\CSharpCallGraphAnalyzer.McpServer.exe"
    }
  }
}
```

### Multiple MCP Servers
```json
{
  "mcpServers": {
    "csharpener": {
      "command": "C:\\tools\\csharpener\\CSharpCallGraphAnalyzer.McpServer.exe"
    },
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "C:\\Users\\username"]
    }
  }
}
```

### With Environment Variables
```json
{
  "mcpServers": {
    "csharpener": {
      "command": "C:\\tools\\csharpener\\CSharpCallGraphAnalyzer.McpServer.exe",
      "env": {
        "LOG_LEVEL": "debug"
      }
    }
  }
}
```

## Performance Tips

1. **First Run**: Initial analysis creates a cache, subsequent runs are faster
2. **Large Solutions**: Exclude test projects and generated code
3. **Background Analysis**: Keep Claude Desktop running for instant access
4. **Cache Location**: Cache files stored in `.csharpener-cache/` in solution directory

## Updates

### Updating the Server

1. Pull latest code:
```bash
git pull origin main
```

2. Republish:
```bash
.\deploy-mcp.ps1
```

3. Restart Claude Desktop (no config change needed if path is same)

### Versioning

Check server version:
```bash
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | dotnet run --project CSharpCallGraphAnalyzer.McpServer
```

Look for `serverInfo.version` in the response.

## Security Considerations

- The MCP server has **read-only access** to your filesystem
- It only analyzes C# code, doesn't execute it
- Runs locally, no data sent to external servers
- Uses stdio communication, no network ports opened

## Support

- **Issues**: https://github.com/lawless-m/CSharpener/issues
- **Discussions**: https://github.com/lawless-m/CSharpener/discussions
- **Documentation**: See README.md files in the project
