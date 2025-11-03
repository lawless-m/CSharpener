# Deploy CSharpener MCP Server
# This script publishes the MCP server as a self-contained executable

param(
    [string]$OutputPath = "$PSScriptRoot\published\mcp-server",
    [string]$Runtime = "win-x64"  # Options: win-x64, linux-x64, osx-x64, osx-arm64
)

Write-Host "Publishing CSharpener MCP Server..." -ForegroundColor Green
Write-Host "Runtime: $Runtime" -ForegroundColor Cyan
Write-Host "Output: $OutputPath" -ForegroundColor Cyan

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

# Publish the project
dotnet publish "$PSScriptRoot\CSharpCallGraphAnalyzer.McpServer\CSharpCallGraphAnalyzer.McpServer.csproj" `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $OutputPath `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nPublish successful!" -ForegroundColor Green
    Write-Host "`nExecutable location:" -ForegroundColor Yellow

    if ($Runtime.StartsWith("win")) {
        $exePath = Join-Path $OutputPath "CSharpCallGraphAnalyzer.McpServer.exe"
        Write-Host $exePath -ForegroundColor White

        Write-Host "`nAdd this to your Claude Desktop config:" -ForegroundColor Yellow
        Write-Host @"
{
  "mcpServers": {
    "csharpener": {
      "command": "$($exePath -replace '\\', '\\')"
    }
  }
}
"@ -ForegroundColor White
    } else {
        $exePath = Join-Path $OutputPath "CSharpCallGraphAnalyzer.McpServer"
        Write-Host $exePath -ForegroundColor White

        # Make executable on Unix
        if ($Runtime.StartsWith("linux") -or $Runtime.StartsWith("osx")) {
            Write-Host "`nMaking executable..." -ForegroundColor Yellow
            chmod +x $exePath
        }

        Write-Host "`nAdd this to your Claude Desktop config:" -ForegroundColor Yellow
        Write-Host @"
{
  "mcpServers": {
    "csharpener": {
      "command": "$exePath"
    }
  }
}
"@ -ForegroundColor White
    }

    Write-Host "`nConfig file location:" -ForegroundColor Yellow
    if ($Runtime.StartsWith("win")) {
        Write-Host "$env:APPDATA\Claude\claude_desktop_config.json" -ForegroundColor White
    } elseif ($Runtime.StartsWith("osx")) {
        Write-Host "~/Library/Application Support/Claude/claude_desktop_config.json" -ForegroundColor White
    } else {
        Write-Host "~/.config/Claude/claude_desktop_config.json" -ForegroundColor White
    }
} else {
    Write-Host "`nPublish failed!" -ForegroundColor Red
    exit 1
}
