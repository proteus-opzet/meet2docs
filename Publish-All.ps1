# >>> set your release version here <<<
$Version = "0.3.0"


# Meet2Docs.Core
Remove-Item ".\Meet2Docs.Core\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\Meet2Docs.Core\obj" -Recurse -Force -ErrorAction SilentlyContinue

# Meet2Docs.Cli
Remove-Item ".\Meet2Docs.Cli\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\Meet2Docs.Cli\obj" -Recurse -Force -ErrorAction SilentlyContinue

# Meet2Docs.Gui
Remove-Item ".\Meet2Docs.Gui\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item ".\Meet2Docs.Gui\obj" -Recurse -Force -ErrorAction SilentlyContinue

# CLI
dotnet publish -c Release -p:PublishProfile=".\Meet2Docs.Cli\Properties\PublishProfiles\FolderProfile-windows-x64.pubxml" .\Meet2Docs.Cli\Meet2Docs.Cli.csproj
dotnet publish -c Release -p:PublishProfile=".\Meet2Docs.Cli\Properties\PublishProfiles\FolderProfile-linux-x64.pubxml"   .\Meet2Docs.Cli\Meet2Docs.Cli.csproj
dotnet publish -c Release -p:PublishProfile=".\Meet2Docs.Cli\Properties\PublishProfiles\FolderProfile-osx-x64.pubxml"     .\Meet2Docs.Cli\Meet2Docs.Cli.csproj

# GUI
dotnet publish -c Release -p:PublishProfile=".\Meet2Docs.Gui\Properties\PublishProfiles\FolderProfile-windows-x64.pubxml" .\Meet2Docs.Gui\Meet2Docs.Gui.csproj
dotnet publish -c Release -p:PublishProfile=".\Meet2Docs.Gui\Properties\PublishProfiles\FolderProfile-linux-x64.pubxml"   .\Meet2Docs.Gui\Meet2Docs.Gui.csproj
dotnet publish -c Release -p:PublishProfile=".\Meet2Docs.Gui\Properties\PublishProfiles\FolderProfile-osx-x64.pubxml"     .\Meet2Docs.Gui\Meet2Docs.Gui.csproj


# Publish root for CLI (net9.0)
$CliPublishRoot = Join-Path $PSScriptRoot "Meet2Docs.Cli\bin\Release\net9.0\publish"

# Source files (single-file outputs created by the pubxml profiles)
$SrcLinux = Join-Path $CliPublishRoot "linux-x64\Meet2Docs.Cli"
$SrcOsx   = Join-Path $CliPublishRoot "osx-x64\Meet2Docs.Cli"
$SrcWin   = Join-Path $CliPublishRoot "win-x64\Meet2Docs.Cli.exe"

# Target file names you want for the release
$DstLinux = Join-Path $CliPublishRoot ("meet2Docs-{0}-linux-x64" -f $Version)
$DstOsx   = Join-Path $CliPublishRoot ("meet2Docs-{0}-osx-x64"   -f $Version)
$DstWin   = Join-Path $CliPublishRoot ("meet2Docs-{0}-win-x64.exe" -f $Version)

# Copy/overwrite if present
if (Test-Path $SrcLinux) { Copy-Item $SrcLinux $DstLinux -Force }
if (Test-Path $SrcOsx)   { Copy-Item $SrcOsx   $DstOsx   -Force }
if (Test-Path $SrcWin)   { Copy-Item $SrcWin   $DstWin   -Force }

Write-Host "Created:"
Write-Host "  $DstLinux"
Write-Host "  $DstOsx"
Write-Host "  $DstWin"