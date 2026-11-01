# Mod Automation, Packaging & Deployment Guide

Developer scripts for building, versioning, packaging, and publishing releases to GitHub, **Thunderstore**, and **Nexus Mods**.

The automation logic is in the shared PowerShell 7 module [**`DW.ValheimModTools`**](https://github.com/DreamWraith/DW-ValheimModTools).

---

## Directory Index

| Script | Purpose |
| :--- | :--- |
| `modtools.ps1` | Single entry point for all release, packaging, and portal publishing commands. Auto-loads `DW.ValheimModTools` from your local clone or GitHub cache. |

> [!NOTE]
> All automation scripts require **PowerShell 7+ (`pwsh`)**.

---

## 1. Release Management (`modtools.ps1 release`)

`modtools.ps1 release` coordinates building, packaging, and publishing releases. It operates locally against your local Valheim game assemblies and uses the [GitHub CLI (`gh`)](https://cli.github.com/).

### Versioning Workflow: Existing Version (Default) vs. Automated Bump

By default, `modtools.ps1 release` uses whatever version is currently defined in your `.csproj` without modifying it. Use this if you bump `<Version>` manually in your project file and write the corresponding release notes in `CHANGELOG.md` before releasing:

```powershell
# Default: Release using the existing version already in .csproj (creates DRAFT on GitHub)
pwsh ./.scripts/modtools.ps1 release

# Optional: Automatically bump patch (e.g. 1.0.0 -> 1.0.1) in .csproj before releasing
pwsh ./.scripts/modtools.ps1 release -Bump Patch

# Optional: Bump minor (1.0.1 -> 1.1.0) or major (1.1.0 -> 2.0.0)
pwsh ./.scripts/modtools.ps1 release -Bump Minor
pwsh ./.scripts/modtools.ps1 release -Bump Major

# Optional: Set an explicit version string
pwsh ./.scripts/modtools.ps1 release -SetVersion "1.2.0"
```

*By default, releases are created in **DRAFT** status on GitHub so you can review notes and attached assets before going live. Publishing triggers the GitHub Action to deploy to Thunderstore and Nexus Mods.*

### Direct Release (Without Draft)

Pass `-Publish` (or `-NoDraft`) to create a published release immediately:

```powershell
pwsh ./.scripts/modtools.ps1 release -Bump Patch -Publish
```

### Dry Run

```powershell
pwsh ./.scripts/modtools.ps1 release -Bump Patch -DryRun
```

### Parameters Reference (`modtools.ps1 release`)

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `-Bump` | `Patch`, `Minor`, `Major` | *None* | Increments SemVer in `.csproj` before building. |
| `-SetVersion` | `string` | *None* | Explicit version string to assign before building. |
| `-Publish` / `-NoDraft` | `switch` | `false` | Publishes the release immediately instead of creating a Draft. |
| `-Configuration` | `string` | `Release` | Target build configuration. |
| `-DryRun` | `switch` | `false` | Assembles packages and previews the `gh release create` command without executing it. |

---

## 2. Packaging & Version Bumping (`modtools.ps1 package`)

`modtools.ps1 package` handles standalone packaging, semantic version bumping, and source archive creation:

```powershell
# Bump version in .csproj and manifest.json without releasing
pwsh ./.scripts/modtools.ps1 package -Bump Patch
pwsh ./.scripts/modtools.ps1 package -SetVersion "1.1.0"

# Assemble distribution zip for current build
pwsh ./.scripts/modtools.ps1 package
```

### Version Management

Version numbers are maintained in exactly one place: the `<Version>` tag in your `.csproj` file.
When building or packaging:

1. **In-Code Plugin Version**: The `GenerateVersionInfo` MSBuild target emits `obj/VersionInfo.g.cs` containing `VersionInfo.Version`. `Plugin.cs` references this directly.
2. **Manifest Sync**: `manifest.json` is automatically updated with the current version and project metadata.
3. **Release Notes**: Notes matching the version section in `CHANGELOG.md` are extracted to `bin/Publish/release_notes.md`.

---

## 3. Direct Portal Publishing (`modtools.ps1 publish`)

If you want to publish directly to Thunderstore or Nexus Mods from your local terminal without GitHub Releases:

```powershell
# Publish to all configured portals
pwsh ./.scripts/modtools.ps1 publish -Target All

# Publish to a single portal
pwsh ./.scripts/modtools.ps1 publish -Target Thunderstore
pwsh ./.scripts/modtools.ps1 publish -Target Nexus

# Dry-run validation
pwsh ./.scripts/modtools.ps1 publish -Target All -DryRun
```

### Direct Platform Commands

- **Thunderstore**:

  ```powershell
  pwsh ./.scripts/modtools.ps1 thunderstore
  ```

- **Nexus Mods**:

  ```powershell
  pwsh ./.scripts/modtools.ps1 nexus
  ```

---

## 4. How `DW.ValheimModTools` Integration Works

`modtools.ps1` resolves the shared PowerShell 7 module dynamically:

1. **Explicit Environment Override**: If `$env:MODTOOLS_PATH` or `$env:DW_VALHEIM_MODTOOLS_PATH` is set, `modtools.ps1` loads that copy directly.
2. **Sibling Repository (Workspace Relative)**: If `DW-ValheimModTools` is cloned in the same parent workspace directory alongside your mod repository, it imports it directly from source.
3. **System-Installed Module**: Checks if `DW.ValheimModTools` is installed in `$env:PSModulePath`.
4. **GitHub Cache Fallback**: If running on a clean machine or in CI, it automatically clones or fast-pulls `master` from `https://github.com/DreamWraith/DW-ValheimModTools.git` into your local cache directory (`%LOCALAPPDATA%\DW-ValheimModTools` on Windows or `~/.cache/DW-ValheimModTools` on Linux/macOS).

---

## 5. GitHub Actions CI/CD Workflow

An automated portal publisher workflow is provided at [`.github/workflows/publish.yml`](../.github/workflows/publish.yml).

### Required & Optional GitHub Secrets

Configure these under **Settings > Secrets and variables > Actions**:

| Secret / Variable | Required | Description |
| :--- | :--- | :--- |
| `COMMUNITY_NAMESPACE` | Recommended | Mod author or team namespace for Thunderstore (e.g., `MyTeam`). |
| `THUNDERSTORE_TOKEN` | Optional | Service account token from [thunderstore.io](https://thunderstore.io). |
| `NEXUSMODS_API_KEY` | Optional | Personal account API key from [nexusmods.com](https://www.nexusmods.com/users/myaccount?tab=api+keys). (Mod ID is read automatically from `<NexusModId>` in your `.csproj`). |
| `COMMUNITY` | Optional | Community slug on Thunderstore (defaults to `valheim`). |
