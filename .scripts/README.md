# Developer Automation & Scripts Guide

This directory (`.scripts/`) contains all developer automation tools for managing SemVer versioning, packaging distribution archives, creating GitHub Releases, and deploying to **Thunderstore** and **Hexium**.

---

## Directory Index

| Script | Purpose |
| :--- | :--- |
| `release.ps1` | Primary release coordinator: compiles in `Release`, packages distribution archive, extracts changelog notes, and publishes GitHub Releases (Draft by default, or published with `-Publish`). |
| `package.ps1` | Standalone packaging engine: semantic version bumping, manifest sync, source archive generation, and distribution archive creation. |
| `publish.ps1` | Master publisher wrapper for direct Thunderstore and Hexium API upload without GitHub Actions. |
| `publish-thunderstore.ps1` | Standalone script for direct Thunderstore API submission (`thunderstore.io`). |
| `publish-hexium.ps1` | Standalone script for direct Hexium API submission (`hexium.gg`). |

---

## 1. Release Management (`release.ps1`)

[`release.ps1`](release.ps1) is the recommended all-in-one command for publishing releases. It operates 100% locally against your local Valheim game assemblies and uses the [GitHub CLI (`gh`)](https://cli.github.com/).

### Versioning Workflow: Existing Version (Default) vs. Automated Bump
By default, `release.ps1` uses whatever version is currently defined in your `.csproj` without modifying it. This is ideal if you bump `<Version>` manually in your project file and write the corresponding release notes in `CHANGELOG.md` before releasing:
```powershell
# Default: Release using the existing version already in .csproj
pwsh ./.scripts/release.ps1

# Optional: Automatically bump patch (e.g. 1.0.1 -> 1.0.2) in .csproj before releasing
pwsh ./.scripts/release.ps1 -Bump Patch

# Optional: Bump minor (1.0.1 -> 1.1.0) or major (1.1.0 -> 2.0.0)
pwsh ./.scripts/release.ps1 -Bump Minor
pwsh ./.scripts/release.ps1 -Bump Major

# Optional: Set an explicit version string
pwsh ./.scripts/release.ps1 -SetVersion "1.2.0"
```
*By default, releases are created in **DRAFT** status on GitHub so you can review notes and attached assets before going live. Publishing triggers the GitHub Action to deploy to Thunderstore and Hexium.*

### Direct Release (Without Draft)
Pass `-Publish` (or `-NoDraft`) to create a published release immediately:
```powershell
pwsh ./.scripts/release.ps1 -Bump Patch -Publish
```

### Dry Run
```powershell
pwsh ./.scripts/release.ps1 -Bump Patch -DryRun
```

### Parameters Reference (`release.ps1`)

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-Bump` | `Patch`, `Minor`, `Major` | *None* | Increments SemVer in `.csproj` before building. |
| `-SetVersion` | `string` | *None* | Explicit version string to assign before building. |
| `-Publish` / `-NoDraft` | `switch` | `false` | Publishes the release immediately instead of creating a Draft. |
| `-Configuration` | `string` | `Release` | Target build configuration. |
| `-NoBuild` | `switch` | `false` | Skips `dotnet build` and packages existing build artifacts. |
| `-Title` | `string` | `v<Version>` | Custom title override for the GitHub Release. |
| `-SyncSecrets` | `switch` | `false` | Forces re-synchronization of local environment variables to GitHub Actions secrets. |
| `-DryRun` | `switch` | `false` | Assembles packages and previews the `gh release create` command without executing it. |

---

## 2. Packaging & Version Bumping (`package.ps1`)

[`package.ps1`](package.ps1) handles standalone packaging, semantic version bumping, and source archive creation:

```powershell
# Bump version in .csproj and manifest.json without releasing
pwsh ./.scripts/package.ps1 -Bump Patch
pwsh ./.scripts/package.ps1 -SetVersion "1.1.0"

# Assemble distribution zip for current build
pwsh ./.scripts/package.ps1

# Assemble package and generate a git source snapshot archive
pwsh ./.scripts/package.ps1 -CreateSourceArchive
```

### Single Source of Truth (SSOT) Versioning
Version numbers are maintained in exactly one place: the `<Version>` tag in your `.csproj` file.
When building or running `package.ps1`:
1. **In-Code Plugin Version**: The `GenerateVersionInfo` MSBuild target emits `obj/VersionInfo.g.cs` containing `VersionInfo.Version`. `Plugin.cs` references this directly.
2. **Manifest Sync**: `manifest.json` is automatically updated with the current version and project metadata.
3. **Release Notes**: Notes matching the version section in `CHANGELOG.md` are extracted to `bin/Publish/release_notes.md`.

### Distribution Package Layout
Generated packages are saved to `bin/Publish/`:
```text
bin/Publish/
├── ConsoleStdinHeeler-<Version>.zip          # Mod package (DLL, manifest.json, icon.png, README, CHANGELOG)
├── ConsoleStdinHeeler-<Version>-Source.zip   # Git source snapshot (via git archive HEAD)
└── release_notes.md                          # Excerpted changelog notes for GitHub Releases
```

---

## 3. Direct Portal Publishing (`publish.ps1`)

If you want to publish directly to Thunderstore or Hexium from your local terminal without GitHub Releases:

```powershell
# Publish to both portals
pwsh ./.scripts/publish.ps1 -Target All

# Publish to a single portal
pwsh ./.scripts/publish.ps1 -Target Thunderstore
pwsh ./.scripts/publish.ps1 -Target Hexium

# Dry-run validation
pwsh ./.scripts/publish.ps1 -Target All -DryRun
```

### Standalone Platform Uploaders
- **Thunderstore**:
  ```powershell
  $env:THUNDERSTORE_TOKEN = "your_token"
  $env:COMMUNITY_NAMESPACE = "dreamwraith"
  pwsh ./.scripts/publish-thunderstore.ps1
  ```
- **Hexium**:
  ```powershell
  $env:HEXIUM_TOKEN = "your_token"
  $env:COMMUNITY_NAMESPACE = "dreamwraith"
  pwsh ./.scripts/publish-hexium.ps1
  ```

---

## 4. GitHub Actions CI/CD Workflow

An automated portal publisher workflow is provided at [`.github/workflows/publish.yml`](../.github/workflows/publish.yml).

### How It Operates
1. **Trigger**: Activates when a GitHub Release is published (`release: [published]`). Draft releases do not trigger the workflow until you click **Publish release** on GitHub.
2. **Download Release Package**: Uses `gh release download` to fetch the pre-compiled mod `.zip` attached to the release.
3. **Publishing**: Automatically validates and uploads the package to Thunderstore and Hexium using repository secrets.

### Required & Optional GitHub Secrets

Configure these under **Settings > Secrets and variables > Actions**:

| Secret | Required | Description |
| :--- | :--- | :--- |
| `COMMUNITY_NAMESPACE` | Recommended | Mod author or team namespace for Thunderstore and Hexium (e.g., `dreamwraith`). |
| `THUNDERSTORE_TOKEN` | Optional | Service account token from [thunderstore.io](https://thunderstore.io). |
| `HEXIUM_TOKEN` | Optional | API token from [hexium.gg](https://hexium.gg). |
| `THUNDERSTORE_NAMESPACE` | Optional | Overrides `COMMUNITY_NAMESPACE` specifically for Thunderstore if different. |
| `HEXIUM_NAMESPACE` | Optional | Overrides `COMMUNITY_NAMESPACE` specifically for Hexium if different. |
| `COMMUNITY` | Optional | Community slug on Thunderstore/Hexium (defaults to `valheim`). |

*(Note: `GITHUB_TOKEN` is automatically provided by GitHub Actions).*
