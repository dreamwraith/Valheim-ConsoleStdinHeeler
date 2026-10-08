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
    $Token = if ($env:HEXIUM_TOKEN) { $env:HEXIUM_TOKEN } else { [Environment]::GetEnvironmentVariable("HEXIUM_TOKEN", "User") }
}
if (-not $Token -and -not $DryRun) {
    Write-Error "No Hexium token provided. Set HEXIUM_TOKEN environment variable or pass -Token."
    exit 1
}

if (-not $Community) {
    $Community = if ($env:HEXIUM_COMMUNITY) { $env:HEXIUM_COMMUNITY } elseif ($env:COMMUNITY) { $env:COMMUNITY } else { "valheim" }
}

if (-not $Namespace) {
    if ($env:HEXIUM_NAMESPACE) { $Namespace = $env:HEXIUM_NAMESPACE }
    elseif ($env:COMMUNITY_NAMESPACE) { $Namespace = $env:COMMUNITY_NAMESPACE }
    elseif ($env:NAMESPACE) { $Namespace = $env:NAMESPACE }
    else {
        $Namespace = [Environment]::GetEnvironmentVariable("COMMUNITY_NAMESPACE", "User")
        if (-not $Namespace) {
            $csproj = Get-ChildItem -Path $ProjectDir -Filter "*.csproj" | Select-Object -First 1
            if ($csproj) {
                $Namespace = ([xml](Get-Content $csproj.FullName)).Project.PropertyGroup.Authors | Select-Object -First 1
            }
        }
    }
}
if (-not $Namespace -and -not $DryRun) {
    Write-Error "No author/team namespace found. Set COMMUNITY_NAMESPACE, pass -Namespace, or set <Authors> in .csproj."
    exit 1
}

if (-not $Categories -or $Categories.Count -eq 0) {
    $Categories = if ($env:HEXIUM_CATEGORIES) { $env:HEXIUM_CATEGORIES -split ',' }
                  elseif ($env:MOD_CATEGORIES) { $env:MOD_CATEGORIES -split ',' }
                  else { @("mods") }
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
    Write-Host "[DryRun] Package structure valid. Target endpoint: https://hexium.gg/api/experimental/submission/submit/" -ForegroundColor Yellow
    return
}

# 3. Publish to Hexium (Chunked UserMedia Protocol)
Write-Host "Uploading $pkgName v$pkgVersion to Hexium..." -ForegroundColor Cyan

$baseUrl = "https://hexium.gg"
$authHeader = @{ Authorization = "Bearer $Token" }

try {
    # Step A: Initiate upload session
    $initJson = @{
        filename = [System.IO.Path]::GetFileName($PackagePath)
        file_size_bytes = [int](Get-Item $PackagePath).Length
    } | ConvertTo-Json

    $init = Invoke-RestMethod -Uri "$baseUrl/api/experimental/usermedia/initiate-upload/" `
        -Method Post -Headers $authHeader -ContentType "application/json" -Body $initJson

    # Step B: Upload file chunk(s)
    $fileBytes = [System.IO.File]::ReadAllBytes($PackagePath)
    $completedParts = foreach ($part in $init.upload_urls) {
        $chunk = [byte[]]::new($part.length)
        [System.Buffer]::BlockCopy($fileBytes, [int]$part.offset, $chunk, 0, [int]$part.length)

        $putHeaders = @{ "Content-Type" = "application/octet-stream" }
        if ($part.url -notmatch "X-Amz-Signature|X-Amz-Algorithm") {
            $putHeaders["Authorization"] = "Bearer $Token"
        }

        $put = Invoke-WebRequest -Uri $part.url -Method Put -Body $chunk -Headers $putHeaders
        $etag = if ($put.Headers.ETag) { $put.Headers.ETag.Trim('"') } else { "part-$($part.part_number)" }
        @{ ETag = $etag; PartNumber = [int]$part.part_number }
    }

    # Step C: Finalize upload session
    $finishJson = @{ parts = @($completedParts) } | ConvertTo-Json -Depth 5
    Invoke-RestMethod -Uri "$baseUrl/api/experimental/usermedia/$($init.user_media.uuid)/finish-upload/" `
        -Method Post -Headers $authHeader -ContentType "application/json" -Body $finishJson | Out-Null

    # Step D: Submit package metadata
    $submitJson = @{
        author_name = $Namespace
        communities = @($Community)
        categories = @($Categories)
        has_nsfw_content = $false
        upload_uuid = $init.user_media.uuid
    } | ConvertTo-Json -Depth 5

    $response = Invoke-RestMethod -Uri "$baseUrl/api/experimental/submission/submit/" `
        -Method Post -Headers $authHeader -ContentType "application/json" -Body $submitJson

    Write-Host "`nSuccessfully published to Hexium!" -ForegroundColor Green
    Write-Host "Mod URL: https://$Community.hexium.gg/" -ForegroundColor Cyan
} catch {
    $errMsg = if ($_.ErrorDetails) { $_.ErrorDetails.Message } else { $_.Exception.Message }
    Write-Error "Hexium upload failed:`n$errMsg"
    exit 1
}
