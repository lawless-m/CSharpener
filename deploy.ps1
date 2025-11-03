# Deploy CSharpener
# Publishes CSharpener as a self-contained executable to Y:\CSharpDLLs\CSharpener\
# Works as both a CLI tool and MCP server

param(
    [string]$OutputPath = "Y:\CSharpDLLs\CSharpener",
    [string]$Runtime = "win-x64"  # Options: win-x64, linux-x64, osx-x64, osx-arm64
)

Write-Host "Publishing CSharpener..." -ForegroundColor Green
Write-Host "Runtime: $Runtime" -ForegroundColor Cyan
Write-Host "Output: $OutputPath" -ForegroundColor Cyan

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

# Use temporary directory for build
$tempPath = "$PSScriptRoot\published\temp"
New-Item -ItemType Directory -Force -Path $tempPath | Out-Null

# Publish the project
dotnet publish "$PSScriptRoot\CSharpCallGraphAnalyzer.McpServer\CSharpCallGraphAnalyzer.McpServer.csproj" `
    --configuration Release `
    --runtime $Runtime `
    --self-contained true `
    --output $tempPath `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nPublish successful!" -ForegroundColor Green

    # Copy and rename to final location
    if ($Runtime.StartsWith("win")) {
        $sourceExe = Join-Path $tempPath "CSharpCallGraphAnalyzer.McpServer.exe"
        $finalExe = Join-Path $OutputPath "CSharpener.exe"

        Copy-Item $sourceExe $finalExe -Force
        Write-Host "`nCopied to: $finalExe" -ForegroundColor Green

        # Clean up temp
        Remove-Item $tempPath -Recurse -Force

        Write-Host "`nExecutable location:" -ForegroundColor Yellow
        Write-Host $finalExe -ForegroundColor White

        Write-Host "`nAdd this to your Claude Desktop config:" -ForegroundColor Yellow
        Write-Host @"
{
  "mcpServers": {
    "csharpener": {
      "command": "$($finalExe -replace '\\', '\\')"
    }
  }
}
"@ -ForegroundColor White
    } else {
        $sourceExe = Join-Path $tempPath "CSharpCallGraphAnalyzer.McpServer"
        $finalExe = Join-Path $OutputPath "CSharpener"

        Copy-Item $sourceExe $finalExe -Force
        chmod +x $finalExe

        # Clean up temp
        Remove-Item $tempPath -Recurse -Force

        Write-Host "`nCopied to: $finalExe" -ForegroundColor Green
        Write-Host "`nExecutable location:" -ForegroundColor Yellow
        Write-Host $finalExe -ForegroundColor White

        Write-Host "`nAdd this to your Claude Desktop config:" -ForegroundColor Yellow
        Write-Host @"
{
  "mcpServers": {
    "csharpener": {
      "command": "$finalExe"
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
