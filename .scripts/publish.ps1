[CmdletBinding()]
param(
    [ValidateSet("All", "Thunderstore", "Hexium")]
    [string]$Target = "All",

    [ValidateSet("Patch", "Minor", "Major")]
    [string]$Bump,

    [string]$SetVersion,

    [string]$ThunderstoreToken,

    [string]$HexiumToken,

    [string]$Community,

    [string]$Namespace,

    [string[]]$Categories,

    [string]$Configuration = "Release",

    [switch]$NoBuild,

    [switch]$DryRun,

    [string]$ProjectDir
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot

if (-not $ProjectDir) {
    $ProjectDir = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path }
}

# 1. Resolve Tokens, Community, and Namespace
if (-not $ThunderstoreToken) {
    $ThunderstoreToken = if ($env:THUNDERSTORE_TOKEN) { $env:THUNDERSTORE_TOKEN } else { $env:TS_TOKEN }
}
if (-not $HexiumToken) {
    $HexiumToken = $env:HEXIUM_TOKEN
}

if (-not $Community) {
    $Community = if ($env:COMMUNITY) { $env:COMMUNITY } else { "valheim" }
}

if (-not $Namespace) {
    if ($env:COMMUNITY_NAMESPACE) { $Namespace = $env:COMMUNITY_NAMESPACE }
    elseif ($env:PUBLISH_NAMESPACE) { $Namespace = $env:PUBLISH_NAMESPACE }
    elseif ($env:MOD_NAMESPACE) { $Namespace = $env:MOD_NAMESPACE }
    elseif ($env:NAMESPACE) { $Namespace = $env:NAMESPACE }
    else {
        # Fallback: Extract Authors from .csproj if available
        $csproj = Get-ChildItem -Path $ProjectDir -Filter "*.csproj" | Select-Object -First 1
        if ($csproj) {
            [xml]$xml = Get-Content $csproj.FullName
            $authors = ($xml.Project.PropertyGroup | Where-Object { $_.Authors } | Select-Object -First 1).Authors
            if ($authors) { $Namespace = $authors }
        }
    }
}

if (-not $Categories -or $Categories.Count -eq 0) {
    if ($env:MOD_CATEGORIES) {
        $Categories = $env:MOD_CATEGORIES -split ',' | ForEach-Object { $_.Trim() }
    } else {
        $Categories = @("mods")
    }
}

Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host " Mod Publisher -> Target: $Target ($Configuration)" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta

# 2. Optionally Bump Version & Build
if ($Bump -or $SetVersion) {
    Write-Host "`n[Step 1/3] Bumping version..." -ForegroundColor Cyan
    $packageArgs = @{
        Configuration = $Configuration
        ProjectDir    = $ProjectDir
    }
    if ($Bump) { $packageArgs["Bump"] = $Bump }
    if ($SetVersion) { $packageArgs["SetVersion"] = $SetVersion }
    & (Join-Path $scriptDir "package.ps1") @packageArgs
}

if (-not $NoBuild) {
    Write-Host "`n[Step 2/3] Building solution in $Configuration mode..." -ForegroundColor Cyan
    dotnet build $ProjectDir -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed. Aborting publishing."
        exit 1
    }
}

# 3. Locate Latest Distribution Package
$publishDir = Join-Path $ProjectDir "bin\Publish"
$latestZip = Get-ChildItem -Path $publishDir -Filter "*.zip" -ErrorAction SilentlyContinue |
             Where-Object { $_.Name -notlike "*-Source.zip" } |
             Sort-Object LastWriteTime -Descending |
             Select-Object -First 1

if (-not $latestZip) {
    Write-Host "No existing zip found in bin\Publish, generating package..." -ForegroundColor Cyan
    $packageScript = if (Test-Path (Join-Path $scriptDir "package.ps1")) { Join-Path $scriptDir "package.ps1" } else { Join-Path $ProjectDir "package.ps1" }
    & $packageScript -Configuration $Configuration -ProjectDir $ProjectDir
    $latestZip = Get-ChildItem -Path $publishDir -Filter "*.zip" -ErrorAction SilentlyContinue |
                 Where-Object { $_.Name -notlike "*-Source.zip" } |
                 Sort-Object LastWriteTime -Descending |
                 Select-Object -First 1
}

if (-not $latestZip) {
    Write-Error "Failed to locate or produce a package zip in '$publishDir'."
    exit 1
}

$packagePath = $latestZip.FullName
Write-Host "`n[Step 3/3] Publishing package: $($latestZip.Name)" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "[DRY RUN MODE ENABLED - No actual network uploads will be made]" -ForegroundColor Yellow
}

$results = [ordered]@{}

# 4. Publish to Thunderstore
if ($Target -in @("All", "Thunderstore")) {
    Write-Host "`n----------------------------------------" -ForegroundColor DarkGray
    Write-Host ">> Publishing to Thunderstore..." -ForegroundColor Cyan
    if (-not $ThunderstoreToken -and -not $DryRun -and $Target -eq "All") {
        Write-Warning "Skipping Thunderstore: No THUNDERSTORE_TOKEN provided."
        $results["Thunderstore"] = "Skipped (No token)"
    } else {
        try {
            $tsArgs = @{
                PackagePath = $packagePath
                Community   = $Community
                Categories  = $Categories
                DryRun      = $DryRun
                ProjectDir  = $ProjectDir
            }
            if ($Namespace) { $tsArgs["Namespace"] = $Namespace }
            if ($ThunderstoreToken) { $tsArgs["Token"] = $ThunderstoreToken }
            & (Join-Path $scriptDir "publish-thunderstore.ps1") @tsArgs
            $results["Thunderstore"] = "Success"
        } catch {
            Write-Error "Thunderstore publication failed: $_"
            $results["Thunderstore"] = "Failed: $_"
        }
    }
}

# 5. Publish to Hexium
if ($Target -in @("All", "Hexium")) {
    Write-Host "`n----------------------------------------" -ForegroundColor DarkGray
    Write-Host ">> Publishing to Hexium..." -ForegroundColor Cyan
    if (-not $HexiumToken -and -not $DryRun -and $Target -eq "All") {
        Write-Warning "Skipping Hexium: No HEXIUM_TOKEN provided."
        $results["Hexium"] = "Skipped (No token)"
    } else {
        try {
            $hexArgs = @{
                PackagePath = $packagePath
                Community   = $Community
                Categories  = $Categories
                DryRun      = $DryRun
                ProjectDir  = $ProjectDir
            }
            if ($Namespace) { $hexArgs["Namespace"] = $Namespace }
            if ($HexiumToken) { $hexArgs["Token"] = $HexiumToken }
            & (Join-Path $scriptDir "publish-hexium.ps1") @hexArgs
            $results["Hexium"] = "Success"
        } catch {
            Write-Error "Hexium publication failed: $_"
            $results["Hexium"] = "Failed: $_"
        }
    }
}

# 6. Final Summary
Write-Host "`n==========================================================" -ForegroundColor Magenta
Write-Host " Publication Summary" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta
foreach ($entry in $results.GetEnumerator()) {
    $color = if ($entry.Value -eq "Success") { "Green" } elseif ($entry.Value -like "Skipped*") { "Yellow" } else { "Red" }
    Write-Host " - $($entry.Key): $($entry.Value)" -ForegroundColor $color
}
Write-Host "==========================================================`n" -ForegroundColor Magenta

$hasFailure = ($results.Values | Where-Object { $_ -like "Failed:*" }).Count -gt 0
if ($hasFailure) {
    exit 1
}
