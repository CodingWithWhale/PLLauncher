# PLLauncher Publish Script
# Creates a standalone .exe and prepares files for the installer

$ProjectDir = "C:\Documents Nikitas\ProgrammingPractice\app\PLLauncher"
$ProjectFile = Join-Path $ProjectDir "PLLauncher\PLLauncher.csproj"
$OutputDir = Join-Path $ProjectDir "dist\publish"

Write-Host "=== PLLauncher Publish ===" -ForegroundColor Cyan

# Clean previous publish
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}

# Step 1: Publish as single-file self-contained executable
Write-Host "`n[1/3] Publishing single-file .exe..." -ForegroundColor Yellow
dotnet publish "$ProjectFile" `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o "$OutputDir"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "  -> Published to: $OutputDir" -ForegroundColor Green

# Step 2: Copy icon to output for the installer
Copy-Item (Join-Path $ProjectDir "PLLauncher\icon.ico") (Join-Path $OutputDir "icon.ico") -Force

# Step 3: Build installer (requires Inno Setup)
$InnoSetupPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (Test-Path $InnoSetupPath) {
    Write-Host "`n[2/3] Building installer..." -ForegroundColor Yellow
    & $InnoSetupPath (Join-Path $ProjectDir "installer.iss")
    Write-Host "  -> Installer created in: $(Join-Path $ProjectDir 'dist')" -ForegroundColor Green
} else {
    Write-Host "`n[2/3] SKIPPED: Inno Setup not found at $InnoSetupPath" -ForegroundColor DarkYellow
    Write-Host "  Install Inno Setup 6 from https://jrsoftware.org/isdl.php" -ForegroundColor DarkYellow
}

Write-Host "`n[3/3] Done!" -ForegroundColor Cyan
Write-Host "Standalone .exe: $OutputDir\PLLauncher.exe" -ForegroundColor Green
