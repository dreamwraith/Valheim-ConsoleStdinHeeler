#requires -Version 7

# 1. Resolve and Load DW.ValheimModTools module
# Priority 1: Local development copy in ~/source/DW-ValheimModTools
$localDev = Join-Path $env:USERPROFILE "source\DW-ValheimModTools\DW.ValheimModTools.psd1"

if (Test-Path $localDev) {
    Import-Module $localDev -Force
} else {
    # Priority 2: System-installed module or cached GitHub clone
    if (Get-Module -ListAvailable DW.ValheimModTools) {
        Import-Module DW.ValheimModTools -Force
    } else {
        $cacheDir = Join-Path $env:LOCALAPPDATA "DW-ValheimModTools"
        $cacheManifest = Join-Path $cacheDir "DW.ValheimModTools.psd1"
        if (-not (Test-Path $cacheManifest)) {
            Write-Host "DW.ValheimModTools not found locally. Cloning from GitHub..." -ForegroundColor Cyan
            git clone --depth 1 https://github.com/DreamWraith/DW-ValheimModTools.git $cacheDir
        } else {
            try { git -C $cacheDir pull --quiet 2>$null } catch { }
        }
        Import-Module $cacheManifest -Force
    }
}

# 2. Parse Command and Sub-arguments
$Command = if ($args.Count -gt 0) { [string]$args[0] } else { $null }
[System.Collections.Generic.List[string]]$restArgs = [System.Collections.Generic.List[string]]::new()
if ($args.Count -gt 1) {
    for ($i = 1; $i -lt $args.Count; $i++) {
        if ($args[$i] -ne $null -and $args[$i] -ne "") {
            $restArgs.Add([string]$args[$i])
        }
    }
}

# 3. Help and Usage Display
if (-not $Command -or $Command -in @("help", "-h", "--help", "/?")) {
    Write-Host "`n==========================================================" -ForegroundColor Cyan
    Write-Host " DreamWraith Valheim Mod Automation (DW.ValheimModTools)" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "`nUsage: pwsh ./.scripts/modtools.ps1 <command> [parameters...]`n" -ForegroundColor Yellow
    Write-Host "Commands:" -ForegroundColor Cyan
    Write-Host "  release       Build, package, sync secrets, and create GitHub Release"
    Write-Host "  package       Bump version, sync manifest, and build release archives"
    Write-Host "  publish       Publish to portals (Thunderstore, Hexium, Nexus Mods)"
    Write-Host "  nexus         Publish directly to Nexus Mods"
    Write-Host "  thunderstore  Publish directly to Thunderstore"
    Write-Host "  hexium        Publish directly to Hexium"
    Write-Host "`nExamples:" -ForegroundColor Cyan
    Write-Host "  pwsh ./.scripts/modtools.ps1 release"
    Write-Host "  pwsh ./.scripts/modtools.ps1 release -Bump Patch"
    Write-Host "  pwsh ./.scripts/modtools.ps1 package -Bump Minor"
    Write-Host "  pwsh ./.scripts/modtools.ps1 publish -Target All -DryRun"
    Write-Host "  pwsh ./.scripts/modtools.ps1 nexus -DryRun"
    Write-Host "==========================================================`n" -ForegroundColor Cyan
    return
}

# 4. Map Command to Cmdlet
$cmdMap = @{
    "release"      = "Invoke-ModRelease"
    "package"      = "New-ModPackage"
    "publish"      = "Publish-ModPackage"
    "nexus"        = "Publish-NexusPackage"
    "thunderstore" = "Publish-ThunderstorePackage"
    "hexium"       = "Publish-HexiumPackage"
}

$targetCmd = $cmdMap[$Command.ToLower()]
if (-not $targetCmd) {
    Write-Error "Unknown command '$Command'. Run 'pwsh ./.scripts/modtools.ps1 help' for usage."
    exit 1
}

# 5. Default Project Directory to parent of .scripts if not explicitly passed
$hasProjectDir = $false
foreach ($arg in $restArgs) {
    if ($arg -match '^-+ProjectDir') {
        $hasProjectDir = $true
        break
    }
}
if (-not $hasProjectDir) {
    $projectRoot = Split-Path $PSScriptRoot -Parent
    $restArgs.Add("-ProjectDir")
    $restArgs.Add($projectRoot)
}

# 6. Execute Cmdlet with Forwarded Arguments
$escapedArgs = $restArgs | ForEach-Object {
    if ($_ -match '[\s"]') {
        '"' + ($_ -replace '"', '`"') + '"'
    } else {
        $_
    }
}
$cmdLine = "$targetCmd " + ($escapedArgs -join " ")
$sb = [scriptblock]::Create($cmdLine)
& $sb


