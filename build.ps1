param (
    [switch]$RunAfterBuild = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " Laptop Forensics System - Build Script" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

$ProjectFile = "src\LaptopForensics.Console\LaptopForensics.Console.csproj"
$PublishDir = "publish"

if (-not (Test-Path $ProjectFile)) {
    Write-Host "ERROR: Could not find project file at $ProjectFile" -ForegroundColor Red
    Write-Host "Please run this script from the repository root." -ForegroundColor Yellow
    exit 1
}

Write-Host "`n[*] Cleaning previous builds..." -ForegroundColor Green
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}
dotnet clean $ProjectFile -c Release

Write-Host "`n[*] Restoring NuGet packages..." -ForegroundColor Green
dotnet restore $ProjectFile

Write-Host "`n[*] Publishing self-contained single-file executable..." -ForegroundColor Green
dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "`n[x] Build failed." -ForegroundColor Red
    exit 1
}

Write-Host "`n[+] Build successful!" -ForegroundColor Green
Write-Host "Executable generated at: $PublishDir\laptop-forensics.exe" -ForegroundColor Green

if ($RunAfterBuild) {
    Write-Host "`n[*] Running Laptop Forensics System (Quick Scan)..." -ForegroundColor Green
    & "$PublishDir\laptop-forensics.exe" --quick
}
