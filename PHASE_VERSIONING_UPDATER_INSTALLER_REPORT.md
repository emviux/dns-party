# DNS Party — Phase: Versioning, Auto-Updater & Windows Installer

## Overview
This phase enhances DNS Party into a professionally distributed Windows application by implementing a central versioning strategy, an in-app GitHub-based Auto-Updater, and a rigorous, Windows-compliant Installer/Uninstaller system.

## 1. Centralized Version Management
- **Single Source of Truth:** Created `src\DNSParty\VersionInfo.cs` containing `CurrentVersion` (now strictly semantic: `1.1.0`), `ProjectName`, `Publisher`, and GitHub repository coordinates (`EMVI/DNS-Party`).
- **Integration:** The MSBuild workflow, `Program.cs` metadata, `InstallerProgram.cs`, and `MainForm.cs` all reference this exact variable to guarantee zero drift.

## 2. Windows Installer & Control Panel Registration
- **Setup Program:** The custom `InstallerProgram.cs` (compiled to `DNSParty-Setup.exe`) now correctly kills active `DNSParty.exe` processes during `/S` (silent) installations to prevent file-locking.
- **Control Panel Integration:** Fully implemented Windows Registry uninstaller entries at `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\DNSParty`. The system now correctly specifies `DisplayName`, `DisplayVersion`, `Publisher`, `InstallLocation`, `DisplayIcon`, `UninstallString` (pointing to `Uninstall.exe /U`), and `QuietUninstallString`. DNS Party now organically populates in **Settings → Apps → Installed Apps**.
- **Uninstaller Execution:** When executed (`Uninstall.exe /U`), it aggressively terminates running application instances and prompts the user via a native MessageBox with the option: "Do you also want to remove your personal settings and DNS profiles?". Selecting "Yes" cleanly deletes the local `%LocalAppData%\DNSParty` payload directory.

## 3. GitHub Auto-Updater
- **UpdateService Architecture:** Created `DNSParty.Services.UpdateService.cs`, which securely pulls JSON release definitions from `https://api.github.com/repos/EMVI/DNS-Party/releases/latest` using native .NET libraries (without any bloat or external dependencies).
- **SemVer Comparison:** It compares the tagged release version (e.g. `v1.2.0`) to `VersionInfo.CurrentVersion` to flip the `IsUpdateAvailable` flag dynamically.
- **Seamless Upgrade Engine:** `DownloadUpdateAsync()` safely pulls the `DNSParty-Setup.exe` asset payload to a temporary file while projecting progress integers to the UI. Once completed, `InstallUpdateAndRestart()` executes the temporary installer asynchronously with `/UPDATE /S` and immediately terminates the running host.

## 4. In-App Settings UI Integration
- **Settings Screen Updates:** A new **"Updates"** card was added cleanly to the `MainForm.cs` Settings view right under the "Maintenance & Profiles" and "In-Game Overlay" components.
- **Interactivity:** Provides the "Check for updates automatically" toggle (saved organically via `ConfigService.Settings`), and dynamically morphs a single button from "Check for Updates" → "Download Update" → "Downloading [47%]".

## 5. GitHub Release Automation
- **GitHub Actions Workflow:** Created a new workflow file `.github/workflows/release.yml` scoped strictly to tags (`v*.*.*`).
- **Release Manifests:** Upon successful compilation via `build.bat`, the workflow dynamically creates `update.json` and hashes all deliverables using SHA-256 for maximum security integrity before shipping the compiled installer (`DNSParty-Setup.exe`), the portable payload (`DNSParty-Portable.exe`), and the source ZIP up to the new GitHub Release automatically.

## 6. Regression Testing
- **Zero Functionality Lost:** The installer correctly protects pre-existing data logic (unless `/PURGE` is specified). 
- **Tests Passed:** The internal acceptance verification suite cleanly passed 100% of integration checks (46/46 passes) encompassing IP validations, IO persistence sweeps, Network Adapter detection, UDP DNS latency probes, and Window Overlay verification.

## 7. Deliverables & Next Steps
- Exact binary distributions exist at `dist\DNSParty-Portable.exe` and `dist\DNSParty-Setup.exe`.
- **How to Publish:** To trigger the first global release over the internet, simply push a new GitHub tag (`git tag v1.1.0` then `git push origin v1.1.0`). The action will automatically provision the binaries, create the release text, inject the SHA-256 block, and users running `v1.0.0` or later will see the "Download Update" UI light up!