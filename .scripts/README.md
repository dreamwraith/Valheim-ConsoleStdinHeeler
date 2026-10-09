# Mod Automation, Packaging & Deployment Guide

This directory (`.scripts/`) provides developer automation tools for managing SemVer versioning, packaging distribution archives, creating GitHub Releases, and deploying to **Thunderstore**, **Hexium**, and **Nexus Mods**.

Automation logic is powered by the shared PowerShell 7 module [**`DW.ValheimModTools`**](https://github.com/DreamWraith/DW-ValheimModTools).

---

## Directory Index

| Script | Purpose |
| :--- | :--- |
| `modtools.ps1` | Unified entrypoint and dispatch shim for all release, packaging, and portal publishing commands. Auto-loads `DW.ValheimModTools` from your local clone or GitHub cache. |

> [!NOTE]
> All automation scripts require **PowerShell 7+ (`pwsh`)**.

---

## 1. Release Management (`modtools.ps1 release`)

`modtools.ps1 release` is the recommended all-in-one coordinator for publishing releases. It operates locally against your local Valheim game assemblies and uses the [GitHub CLI (`gh`)](https://cli.github.com/).

`powershell
# Default: Release using the existing version already in .csproj (creates DRAFT on GitHub)
pwsh ./.scripts/modtools.ps1 release

# Optional: Automatically bump patch (e.g. 1.0.0 -> 1.0.1) in .csproj before releasing
pwsh ./.scripts/modtools.ps1 release -Bump Patch

# Optional: Bump minor or major
pwsh ./.scripts/modtools.ps1 release -Bump Minor
pwsh ./.scripts/modtools.ps1 release -Bump Major

# Optional: Set an explicit version string
pwsh ./.scripts/modtools.ps1 release -SetVersion "1.2.0"
`

*By default, releases are created in **DRAFT** status on GitHub so you can review notes and attached assets before going live. Publishing triggers the GitHub Action to deploy to Thunderstore, Hexium, and Nexus Mods.*

### Direct Release (Without Draft)
Pass `-Publish` (or `-NoDraft`) to create a published release immediately:
`powershell
pwsh ./.scripts/modtools.ps1 release -Bump Patch -Publish
`

### Dry Run
`powershell
pwsh ./.scripts/modtools.ps1 release -Bump Patch -DryRun
`

---

## 2. Packaging & Version Bumping (`modtools.ps1 package`)

`powershell
# Bump version in .csproj and manifest.json without releasing
pwsh ./.scripts/modtools.ps1 package -Bump Patch

# Assemble distribution zip for current build
pwsh ./.scripts/modtools.ps1 package

# Assemble package and generate a source archive
pwsh ./.scripts/modtools.ps1 package -CreateSourceArchive
`

---

## 3. Direct Portal Publishing (`modtools.ps1 publish`)

`powershell
# Publish to all configured portals
pwsh ./.scripts/modtools.ps1 publish -Target All

# Publish to a single portal
pwsh ./.scripts/modtools.ps1 publish -Target Thunderstore
pwsh ./.scripts/modtools.ps1 publish -Target Hexium
pwsh ./.scripts/modtools.ps1 publish -Target Nexus

# Dry-run validation
pwsh ./.scripts/modtools.ps1 publish -Target All -DryRun
`

### Direct Platform Commands
- **Thunderstore**: `pwsh ./.scripts/modtools.ps1 thunderstore`
- **Hexium**: `pwsh ./.scripts/modtools.ps1 hexium`
- **Nexus Mods**: `pwsh ./.scripts/modtools.ps1 nexus`

---

## 4. How `DW.ValheimModTools` Integration Works

`modtools.ps1` resolves the shared PowerShell 7 module dynamically:
1. **Local Developer Copy First**: If you have cloned `DW-ValheimModTools` to `~/source/DW-ValheimModTools`, `modtools.ps1` immediately imports it from source. Any improvements or bugfixes made to `DW-ValheimModTools` are instantly live across all your mods without any install step.
2. **System-Installed Module**: Checks if `DW.ValheimModTools` is in `C:\Users\slugw\OneDrive\Documents\PowerShell\Modules;C:\Program Files\PowerShell\Modules;c:\program files\windowsapps\microsoft.powershell_7.6.6.0_x64__8wekyb3d8bbwe\Modules;C:\Program Files\WindowsPowerShell\Modules;C:\WINDOWS\system32\WindowsPowerShell\v1.0\Modules`.
3. **GitHub Cache Fallback**: If running on a new machine or in CI, it automatically clones or fast-pulls `master` from `https://github.com/DreamWraith/DW-ValheimModTools.git` into `%LOCALAPPDATA%\DW-ValheimModTools`.

---

## 5. GitHub Actions CI/CD Workflow

An automated portal publisher workflow is located at [`.github/workflows/publish.yml`](../.github/workflows/publish.yml).

### Required & Optional GitHub Secrets

Configure these under **Settings > Secrets and variables > Actions**:

| Secret / Variable | Required | Description |
| :--- | :--- | :--- |
| COMMUNITY_NAMESPACE | Recommended | Mod author or team namespace for Thunderstore and Hexium (e.g., DreamWraith). |
| THUNDERSTORE_TOKEN | Optional | Service account token from [thunderstore.io](https://thunderstore.io). |
| HEXIUM_TOKEN | Optional | API token from [hexium.gg](https://hexium.gg). |
| NEXUSMODS_API_KEY | Optional | Personal account API key from [nexusmods.com](https://www.nexusmods.com/users/myaccount?tab=api+keys). (Mod ID is read automatically from <NexusModId> in your .csproj). |
| COMMUNITY | Optional | Community slug on Thunderstore/Hexium (defaults to alheim). |
