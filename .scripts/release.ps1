[CmdletBinding()]
param(
    [ValidateSet("Patch", "Minor", "Major")]
    [string]$Bump,

    [string]$SetVersion,

    [switch]$Publish,

    [switch]$NoDraft,

    [string]$Configuration = "Release",

    [switch]$NoBuild,

    [string]$Title,

    [switch]$DryRun,

    [switch]$SyncSecrets,

    [string]$ProjectDir
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if (-not $ProjectDir) {
    $ProjectDir = if ($PSScriptRoot) { Split-Path $PSScriptRoot -Parent } else { (Get-Location).Path }
}

if ($NoDraft) {
    $Publish = $true
}

Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host " Mod Release Coordinator (GitHub Release & Assets)" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta

# 1. Verify Prerequisites (gh CLI, git)
$ghCmd = Get-Command gh -ErrorAction SilentlyContinue
if (-not $ghCmd) {
    $standardGhPaths = @(
        "$env:ProgramFiles\GitHub CLI\gh.exe",
        "${env:ProgramFiles(x86)}\GitHub CLI\gh.exe",
        "$env:LOCALAPPDATA\Programs\GitHub CLI\gh.exe"
    )
    foreach ($p in $standardGhPaths) {
        if (Test-Path $p) {
            $env:PATH = "$([System.IO.Path]::GetDirectoryName($p));$env:PATH"
            $ghCmd = Get-Command gh -ErrorAction SilentlyContinue
            break
        }
    }
}
if (-not $ghCmd) {
    Write-Error "GitHub CLI ('gh') is not installed or not in PATH.`nPlease install it from https://cli.github.com/ to use automated releases."
    exit 1
}

$gitCmd = Get-Command git -ErrorAction SilentlyContinue
if (-not $gitCmd) {
    Write-Error "Git CLI ('git') is not installed or not in PATH."
    exit 1
}

try {
    $null = gh auth status 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "GitHub CLI is not authenticated.`nPlease run 'gh auth login' to authenticate with your GitHub account."
        exit 1
    }
} catch {
    Write-Error "Failed to verify GitHub CLI authentication: $_"
    exit 1
}

# 2. Check and Synchronize GitHub Actions Variables & Secrets
Write-Host "`n[Prerequisite] Verifying GitHub Actions variables and secrets..." -ForegroundColor Cyan
try {
    # Public variables (not masked in logs)
    $existingVars = @(& gh variable list --json name -q ".[].name" 2>$null)
    $nsVal = [Environment]::GetEnvironmentVariable("COMMUNITY_NAMESPACE", "Process")
    if (-not $nsVal) { $nsVal = [Environment]::GetEnvironmentVariable("COMMUNITY_NAMESPACE", "User") }
    if ($nsVal) {
        if ($existingVars -notcontains "COMMUNITY_NAMESPACE" -or $SyncSecrets) {
            if ($DryRun) {
                Write-Host "   [DRY RUN] Would sync variable 'COMMUNITY_NAMESPACE' to GitHub repository." -ForegroundColor Yellow
            } else {
                Write-Host "   Syncing variable 'COMMUNITY_NAMESPACE' to GitHub repository..." -ForegroundColor Cyan
                $null = gh variable set COMMUNITY_NAMESPACE --body $nsVal 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "   ✓ Variable 'COMMUNITY_NAMESPACE' synchronized to GitHub." -ForegroundColor Green
                }
            }
        } else {
            Write-Host "   ✓ Variable 'COMMUNITY_NAMESPACE' is configured on GitHub." -ForegroundColor DarkGreen
        }
    }

    # Sensitive tokens (masked in logs)
    $existingSecrets = @(& gh secret list --json name -q ".[].name" 2>$null)
    $candidateSecrets = @("THUNDERSTORE_TOKEN", "HEXIUM_TOKEN")

    foreach ($sec in $candidateSecrets) {
        $val = [Environment]::GetEnvironmentVariable($sec, "Process")
        if (-not $val) {
            $val = [Environment]::GetEnvironmentVariable($sec, "User")
        }
        $isMissingOnGitHub = ($existingSecrets -notcontains $sec)

        if ($val -and ($isMissingOnGitHub -or $SyncSecrets)) {
            if ($DryRun) {
                Write-Host "   [DRY RUN] Would sync secret '$sec' to GitHub repository." -ForegroundColor Yellow
            } else {
                Write-Host "   Syncing secret '$sec' to GitHub repository..." -ForegroundColor Cyan
                $null = gh secret set $sec --body $val 2>&1
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "   ✓ Secret '$sec' synchronized to GitHub." -ForegroundColor Green
                } else {
                    Write-Warning "Could not synchronize secret '$sec' to GitHub."
                }
            }
        } elseif (-not $isMissingOnGitHub) {
            Write-Host "   ✓ Secret '$sec' is configured on GitHub." -ForegroundColor DarkGreen
        } else {
            Write-Host "   - Secret '$sec' is not set locally or on GitHub (portal step will be skipped in CI)." -ForegroundColor DarkGray
        }
    }
} catch {
    Write-Warning "Could not query or synchronize GitHub repository configuration: $_"
}

# 3. Check Git Status (Working Directory)
try {
    $gitStatus = git -C $ProjectDir status --porcelain 2>&1
    if ($gitStatus) {
        Write-Host "`n[Notice] Working tree has uncommitted changes:" -ForegroundColor Yellow
        $gitStatus | ForEach-Object { Write-Host "   $_" -ForegroundColor DarkYellow }
        Write-Host "The release tag will capture committed repository state (HEAD).`n" -ForegroundColor Yellow
    }
} catch { }

# 4. Resolve & Update Project Version
$csprojFile = Get-ChildItem -Path $ProjectDir -Filter "*.csproj" | Select-Object -First 1
if (-not $csprojFile) {
    Write-Error "Could not find a .csproj file in '$ProjectDir'."
    exit 1
}
$csprojPath = $csprojFile.FullName
[xml]$csproj = Get-Content $csprojPath

$versionNode = $csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
$currentVersion = if ($versionNode -and $versionNode.Version) { $versionNode.Version } else { "1.0.0" }
$targetVersion = $currentVersion

if ($SetVersion) {
    $targetVersion = $SetVersion
} elseif ($Bump) {
    $parts = $currentVersion.Split('.')
    [int]$major = if ($parts.Length -gt 0) { [int]$parts[0] } else { 1 }
    [int]$minor = if ($parts.Length -gt 1) { [int]$parts[1] } else { 0 }
    [int]$patch = if ($parts.Length -gt 2) { [int]$parts[2] } else { 0 }

    switch ($Bump) {
        "Major" { $major++; $minor = 0; $patch = 0 }
        "Minor" { $minor++; $patch = 0 }
        "Patch" { $patch++ }
    }
    $targetVersion = "$major.$minor.$patch"
}

if ($targetVersion -ne $currentVersion) {
    Write-Host "Updating version: $currentVersion -> $targetVersion in $($csprojFile.Name)" -ForegroundColor Cyan
    $versionNode.Version = $targetVersion
    $csproj.Save($csprojPath)

    # Keep manifest.json on disk in sync with bumped version
    $manifestFile = Join-Path $ProjectDir "manifest.json"
    if (Test-Path $manifestFile) {
        try {
            $manifestObj = Get-Content $manifestFile -Raw | ConvertFrom-Json
            $manifestObj.version_number = $targetVersion.TrimStart('v')
            $manifestJson = $manifestObj | ConvertTo-Json -Depth 4
            [System.IO.File]::WriteAllText($manifestFile, $manifestJson + [Environment]::NewLine)
            Write-Host "Updated manifest.json to v$($targetVersion.TrimStart('v')) on disk." -ForegroundColor Cyan
        } catch { }
    }
} else {
    Write-Host "Using existing project version: $currentVersion (from $($csprojFile.Name))" -ForegroundColor Cyan
}

$cleanVersion = $targetVersion.TrimStart('v')
$tagName = "v$cleanVersion"
$releaseTitle = if ($Title) { $Title } else { $tagName }

# 5. Verify manifest.json synchronization with project version and release commit
Write-Host "`n[Prerequisite] Validating manifest.json synchronization..." -ForegroundColor Cyan
$manifestPath = Join-Path $ProjectDir "manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Error "Release blocked: 'manifest.json' not found in '$ProjectDir'."
    exit 1
}

$diskManifest = $null
try {
    $diskManifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
} catch {
    Write-Error "Release blocked: Failed to parse 'manifest.json' at '$manifestPath': $_"
    exit 1
}

$diskVersion = $diskManifest.version_number
if ($diskVersion -ne $cleanVersion) {
    Write-Error @"
Release blocked: manifest.json version ('$diskVersion') does not match project version ('$cleanVersion').
When bumping the version number, manifest.json must be updated to match '$cleanVersion' and committed to git before releasing.
"@
    exit 1
}

# Verify manifest.json in git commit (HEAD) and ensure no uncommitted manifest changes
try {
    $isGit = (git -C $ProjectDir rev-parse --is-inside-work-tree 2>$null)
    if ($LASTEXITCODE -eq 0 -and $isGit -eq "true") {
        $headManifestRaw = git -C $ProjectDir show HEAD:manifest.json 2>$null
        if ($LASTEXITCODE -eq 0 -and $headManifestRaw) {
            $headManifest = $headManifestRaw | ConvertFrom-Json
            $headVersion = $headManifest.version_number
            if ($headVersion -ne $cleanVersion) {
                if ($DryRun) {
                    Write-Host "   [DRY RUN] Would block release: manifest.json in git commit (HEAD) is version '$headVersion', but release version is '$cleanVersion'." -ForegroundColor Yellow
                } else {
                    Write-Error @"
Release blocked: manifest.json in git commit (HEAD) is version '$headVersion', but release version is '$cleanVersion'.
All release commits must include the updated manifest.json when the version number is bumped.
Please commit manifest.json before creating a release:
   git add manifest.json $($csprojFile.Name)
   git commit -m "feat: Release v$cleanVersion"
"@
                    exit 1
                }
            }
        }

        $manifestGitDiff = git -C $ProjectDir status --porcelain manifest.json 2>$null
        if ($manifestGitDiff) {
            if ($DryRun) {
                Write-Host "   [DRY RUN] Would block release: manifest.json has uncommitted changes in working tree." -ForegroundColor Yellow
            } else {
                Write-Error @"
Release blocked: manifest.json has uncommitted changes in the working tree.
All release commits must include the updated manifest.json when the version number is bumped.
Please commit manifest.json before creating a release:
   git add manifest.json
   git commit -m "chore: Update manifest.json to v$cleanVersion"
"@
                exit 1
            }
        }
    }
} catch { }

Write-Host "   ✓ manifest.json is synchronized with v$cleanVersion and verified in release commit." -ForegroundColor Green

# Pre-flight check: ensure release tag doesn't already exist on GitHub
try {
    $existingRelease = gh release view $tagName --json url -q .url 2>$null
    if ($LASTEXITCODE -eq 0 -and $existingRelease) {
        Write-Error "A GitHub release for tag '$tagName' already exists: $existingRelease`nTo create a new release, update <Version> in $($csprojFile.Name) or pass -Bump (Patch|Minor|Major)."
        exit 1
    }
} catch { }

# 4. Build Solution
if (-not $NoBuild) {
    Write-Host "`n[Step 1/4] Compiling solution in $Configuration mode for $tagName..." -ForegroundColor Cyan
    dotnet build $ProjectDir -c $Configuration -p:Version=$cleanVersion
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed. Aborting release."
        exit 1
    }
} else {
    Write-Host "`n[Step 1/4] Skipping build (-NoBuild specified)..." -ForegroundColor DarkGray
}

# 5. Package Mod Distribution Archive
Write-Host "`n[Step 2/4] Assembling release distribution package..." -ForegroundColor Cyan
$packageScript = Join-Path $PSScriptRoot "package.ps1"
if (-not (Test-Path $packageScript)) {
    Write-Error "package.ps1 not found at '$packageScript'."
    exit 1
}

& $packageScript -Configuration $Configuration -ProjectDir $ProjectDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Packaging failed. Aborting release."
    exit 1
}

$publishDir = Join-Path $ProjectDir "bin\Publish"
$modZip = Get-ChildItem -Path $publishDir -Filter "*.zip" -ErrorAction SilentlyContinue |
          Where-Object { $_.Name -notlike "*-Source.zip" } |
          Sort-Object LastWriteTime -Descending |
          Select-Object -First 1

if (-not $modZip) {
    Write-Error "Failed to locate distribution zip in '$publishDir'."
    exit 1
}

Write-Host "Mod Package:    $($modZip.FullName)" -ForegroundColor Green

# 6. Extract Release Notes from CHANGELOG.md
Write-Host "`n[Step 3/4] Extracting release notes from CHANGELOG.md for $cleanVersion..." -ForegroundColor Cyan
$changelogFile = Join-Path $ProjectDir "CHANGELOG.md"
$notes = ""
if (Test-Path $changelogFile) {
    $lines = Get-Content $changelogFile
    $inSection = $false
    $excerpt = [System.Collections.Generic.List[string]]::new()
    $escapedVer = [regex]::Escape($cleanVersion)

    foreach ($line in $lines) {
        if ($line -match "^##\s+\[?$escapedVer(\]|\s|$)") {
            $inSection = $true
            continue
        }
        if ($inSection) {
            if ($line -match "^##\s+") {
                break
            }
            $excerpt.Add($line)
        }
    }
    $notes = ($excerpt -join "`n").Trim()
}

if (-not $notes) {
    Write-Host "No matching section for [$cleanVersion] found in CHANGELOG.md; using default notes." -ForegroundColor Yellow
    $notes = "Release $tagName"
}

$notesPath = Join-Path $publishDir "release_notes.md"
[System.IO.File]::WriteAllText($notesPath, $notes + [Environment]::NewLine)

Write-Host "`n=== Excerpted Release Notes ===" -ForegroundColor DarkCyan
Write-Host $notes
Write-Host "===============================" -ForegroundColor DarkCyan

# 7. Create GitHub Release via gh CLI
$isDraft = -not $Publish
$statusDesc = if ($isDraft) { "DRAFT" } else { "PUBLISHED (FULL)" }

Write-Host "`n[Step 4/4] Creating GitHub Release ($statusDesc) for $tagName..." -ForegroundColor Cyan

$ghArgs = @(
    "release", "create", $tagName,
    $modZip.FullName,
    "--title", $releaseTitle,
    "--notes-file", $notesPath
)

if ($isDraft) {
    $ghArgs += "--draft"
}

if ($DryRun) {
    Write-Host "`n[DRY RUN] Would execute:" -ForegroundColor Yellow
    Write-Host "gh $($ghArgs -join ' ')" -ForegroundColor Yellow
    Write-Host "`nDry run complete. No release was created." -ForegroundColor Yellow
    exit 0
}

$releaseUrl = & gh @ghArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "gh release create failed. Please inspect the error output above."
    exit 1
}

# 8. Final Release Summary
Write-Host "`n==========================================================" -ForegroundColor Magenta
Write-Host " Release Complete!" -ForegroundColor Magenta
Write-Host "==========================================================" -ForegroundColor Magenta
Write-Host " Tag:         $tagName" -ForegroundColor Green
Write-Host " Status:      $statusDesc" -ForegroundColor Green
Write-Host " Package:     $($modZip.Name)" -ForegroundColor Green
if ($releaseUrl) {
    Write-Host " URL:         $releaseUrl" -ForegroundColor Cyan
}

if ($isDraft) {
    Write-Host "`n[Next Step - Draft Release]" -ForegroundColor Yellow
    Write-Host "Your release is saved as a DRAFT on GitHub." -ForegroundColor Yellow
    Write-Host "Visit GitHub to review the release notes and attached assets." -ForegroundColor Yellow
    Write-Host "When you click 'Publish release', the GitHub Action will automatically trigger" -ForegroundColor Yellow
    Write-Host "and publish your mod package to Thunderstore and Hexium!`n" -ForegroundColor Yellow
} else {
    Write-Host "`n[Next Step - Full Release]" -ForegroundColor Green
    Write-Host "Your release is PUBLISHED on GitHub." -ForegroundColor Green
    Write-Host "The GitHub Action has been triggered and will publish your mod to Thunderstore and Hexium!`n" -ForegroundColor Green
}
