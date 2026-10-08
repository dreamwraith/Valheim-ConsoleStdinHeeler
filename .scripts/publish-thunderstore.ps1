[CmdletBinding()]
param(
    [string]$PackagePath,
    [string]$Token,
    [string]$Community,
    [string]$Namespace,
    [string[]]$Categories,
    [switch]$DryRun,
    [string]$ProjectDir
)

$ErrorActionPreference = "Stop"

if (-not $ProjectDir) {
    $ProjectDir = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path }
}

# 1. Resolve Credentials and Metadata
if (-not $Token) {
    $Token = if ($env:THUNDERSTORE_TOKEN) { $env:THUNDERSTORE_TOKEN } else { [Environment]::GetEnvironmentVariable("THUNDERSTORE_TOKEN", "User") }
}
if (-not $Token -and -not $DryRun) {
    Write-Error "No Thunderstore token provided. Set THUNDERSTORE_TOKEN environment variable or pass -Token."
    exit 1
}

if (-not $Community) {
    $Community = if ($env:COMMUNITY) { $env:COMMUNITY } else { "valheim" }
}

if (-not $Namespace) {
    $Namespace = if ($env:COMMUNITY_NAMESPACE) { $env:COMMUNITY_NAMESPACE } else { [Environment]::GetEnvironmentVariable("COMMUNITY_NAMESPACE", "User") }
    if (-not $Namespace) {
        $csproj = Get-ChildItem -Path $ProjectDir -Filter "*.csproj" | Select-Object -First 1
        if ($csproj) {
            $Namespace = ([xml](Get-Content $csproj.FullName)).Project.PropertyGroup.Authors | Select-Object -First 1
        }
    }
}
if (-not $Namespace -and -not $DryRun) {
    Write-Error "No author/team namespace found. Set COMMUNITY_NAMESPACE, pass -Namespace, or set <Authors> in .csproj."
    exit 1
}

if (-not $Categories -or $Categories.Count -eq 0) {
    $Categories = if ($env:MOD_CATEGORIES) { $env:MOD_CATEGORIES -split ',' } else { @("mods") }
}

# 2. Locate and Verify Package
if (-not $PackagePath) {
    $latestZip = Get-ChildItem -Path (Join-Path $ProjectDir "bin\Publish") -Filter "*.zip" -ErrorAction SilentlyContinue |
                 Where-Object { $_.Name -notlike "*-Source.zip" } |
                 Sort-Object LastWriteTime -Descending |
                 Select-Object -First 1
    if ($latestZip) { $PackagePath = $latestZip.FullName }
}

if (-not $PackagePath -or -not (Test-Path $PackagePath)) {
    Write-Error "Could not locate package zip. Run package.ps1 first or specify -PackagePath."
    exit 1
}

# Extract name/version from manifest.json
$manifestPath = Join-Path $ProjectDir "manifest.json"
$pkgName = "UnknownMod"
$pkgVersion = "1.0.0"
if (Test-Path $manifestPath) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $pkgName = $manifest.name
    $pkgVersion = $manifest.version_number
}

Write-Host "Package verified: $Namespace/$pkgName v$pkgVersion (Community: $Community)" -ForegroundColor Green

if ($DryRun) {
    Write-Host "[DryRun] Package structure valid. Target endpoint: https://thunderstore.io/api/experimental/submission/upload/" -ForegroundColor Yellow
    return
}

# 3. Publish to Thunderstore
Write-Host "Uploading $pkgName v$pkgVersion to Thunderstore..." -ForegroundColor Cyan

$form = @{
    file = Get-Item $PackagePath
    metadata = @{
        author_name = $Namespace
        communities = @($Community)
        categories = @($Categories)
        has_nsfw_content = $false
    } | ConvertTo-Json
}

try {
    $response = Invoke-RestMethod -Uri "https://thunderstore.io/api/experimental/submission/upload/" `
        -Method Post `
        -Headers @{ Authorization = "Bearer $Token" } `
        -Form $form

    Write-Host "`nSuccessfully published to Thunderstore!" -ForegroundColor Green
    Write-Host "Mod URL: https://thunderstore.io/c/$Community/p/$Namespace/$pkgName/" -ForegroundColor Cyan
} catch {
    $errMsg = if ($_.ErrorDetails) { $_.ErrorDetails.Message } else { $_.Exception.Message }
    Write-Error "Thunderstore upload failed:`n$errMsg"
    exit 1
}
