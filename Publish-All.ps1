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


# --- Paths ---
$SolutionPublishFolder = Join-Path $PSScriptRoot "publish"
if (-not (Test-Path $SolutionPublishFolder)) { New-Item -ItemType Directory -Path $SolutionPublishFolder | Out-Null }

# CLI publish root
$CliPublishRoot = Join-Path $PSScriptRoot "Meet2Docs.Cli\bin\Release\net9.0\publish"
$CliSrcLinux = Join-Path $CliPublishRoot "linux-x64\Meet2Docs.Cli"
$CliSrcOsx   = Join-Path $CliPublishRoot "osx-x64\Meet2Docs.Cli"
$CliSrcWin   = Join-Path $CliPublishRoot "win-x64\Meet2Docs.Cli.exe"

# GUI publish root
$GuiPublishRoot = Join-Path $PSScriptRoot "Meet2Docs.Gui\bin\Release\net9.0\publish"
$GuiSrcLinux = Join-Path $GuiPublishRoot "linux-x64\Meet2Docs.Gui"
$GuiSrcOsx   = Join-Path $GuiPublishRoot "osx-x64\Meet2Docs.Gui"
$GuiSrcWin   = Join-Path $GuiPublishRoot "win-x64\Meet2Docs.Gui.exe"

# --- Rename/copy CLI outputs ---
$CliDstLinux = Join-Path $SolutionPublishFolder ("meet2Docs-cli-{0}-linux-x64" -f $Version)
$CliDstOsx   = Join-Path $SolutionPublishFolder ("meet2Docs-cli-{0}-osx-x64"   -f $Version)
$CliDstWin   = Join-Path $SolutionPublishFolder ("meet2Docs-cli-{0}-win-x64.exe" -f $Version)

if (Test-Path $CliSrcLinux) { Copy-Item $CliSrcLinux $CliDstLinux -Force }
if (Test-Path $CliSrcOsx)   { Copy-Item $CliSrcOsx   $CliDstOsx   -Force }
if (Test-Path $CliSrcWin)   { Copy-Item $CliSrcWin   $CliDstWin   -Force }

# --- Rename/copy GUI outputs ---
$GuiDstLinux = Join-Path $SolutionPublishFolder ("meet2Docs-gui-{0}-linux-x64" -f $Version)
$GuiDstOsx   = Join-Path $SolutionPublishFolder ("meet2Docs-gui-{0}-osx-x64"   -f $Version)
$GuiDstWin   = Join-Path $SolutionPublishFolder ("meet2Docs-gui-{0}-win-x64.exe" -f $Version)

if (Test-Path $GuiSrcLinux) { Copy-Item $GuiSrcLinux $GuiDstLinux -Force }
if (Test-Path $GuiSrcOsx)   { Copy-Item $GuiSrcOsx   $GuiDstOsx   -Force }
if (Test-Path $GuiSrcWin)   { Copy-Item $GuiSrcWin   $GuiDstWin   -Force }

Write-Host "`nCreated in $SolutionPublishFolder"
Get-ChildItem $SolutionPublishFolder