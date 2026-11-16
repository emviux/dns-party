# DNS Party — Windows DNS Manager

[🇮🇷 فارسی (Persian)](README.fa.md)

**DNS Party** is a fast, lightweight, gaming-inspired native Windows desktop utility for managing, testing, and switching between IPv4 and IPv6 DNS server configurations with 1-click System Tray integration.

---

## Key Features

- **1-Click DNS Switching**: Quickly switch between **Cloudflare** (`1.1.1.1`), **Google DNS** (`8.8.8.8`), **Quad9** (`9.9.9.9`), **AdGuard DNS** (`94.140.14.14`), **OpenDNS** (`208.67.222.222`), and unlimited custom IPv4/IPv6 DNS profiles.
- **Real Windows Verification**: Reads actual Windows network adapter state (`NetworkInterface` + `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip(6)\Parameters\Interfaces\{GUID}`) to distinguish requested vs. active DNS and never display a false connection state.
- **In-Game Overlay**: Transparent, borderless DNS switching overlay accessible via global keyboard shortcut (default `Shift+Tab`, configurable). Seamlessly switch DNS profiles without leaving your game. Avoids Steam conflicts.
- **Disable DNS (Restore Automatic DHCP)**: Restores the active Ethernet or Wi-Fi adapter back to Windows automatic DHCP DNS (`netsh interface ipv4/ipv6 set dnsservers ... source=dhcp`) and verifies restoration.
- **Flush DNS Cache**: Executes `ipconfig /flushdns` silently without opening a command prompt window and reports confirmation.
- **Direct UDP Port 53 DNS Latency Tester**: Measures real DNS server response times in milliseconds (`ms`) using RFC 1035 UDP DNS queries.
- **System Tray Quick-Switcher**:
  - Minimizes/closes cleanly to the Windows System Tray.
  - Excludes itself from `Alt+Tab` (`WS_EX_TOOLWINDOW`) and consumes 0% idle CPU while hidden in the background.
  - Right-click menu provides instant switching, checkmark indicator for the active DNS, **User Favorites** submenu, **Disable DNS**, **Flush DNS Cache**, **Settings**, and **Exit**.
  - Left-clicking the tray icon restores the main window.
- **Subtle Connection Chimes**: Plays short, locally generated PCM `.wav` audio effects (`connect.wav` / `disconnect.wav`) when DNS changes succeed (toggleable in Settings).
- **Smart UAC Privileges**: Runs normal UI as a standard user (`asInvoker`) so startup and browsing require no UAC prompt; seamlessly elevates (`--elevated-apply` / `--elevated-dhcp` via `runas`) only when modifying Windows adapter DNS settings.

---

## Project Structure

```text
DNS Party/
├── ARCHITECTURE.md                  # Architecture, stack benchmarks & design decisions
├── README.md                        # Documentation, build & usage instructions
├── build.bat                        # 1-click build and QA test script
├── assets/
│   ├── icon_pack/                   # Supplied SVG icon pack
│   ├── logo_assets/                 # Supplied multi-size PNGs & dns-party.ico
│   └── sounds/                      # Local PCM WAV chimes (connect.wav, disconnect.wav)
├── src/
│   ├── DNSParty/
│   │   ├── DNSParty.csproj          # MSBuild project file (.NET Framework 4.8)
│   │   ├── app.manifest             # PerMonitorV2 DPI & UAC manifest
│   │   ├── Program.cs               # Single-instance entry point & CLI elevation helper
│   │   ├── Models/
│   │   │   ├── DnsProfile.cs        # DNS profile model, defaults & strict IPv4/IPv6 validation
│   │   │   ├── AppSettings.cs       # User settings & profile persistence model
│   │   │   └── DnsStatusSnapshot.cs # Live Windows network adapter DNS state snapshot
│   │   ├── Services/
│   │   │   ├── ConfigService.cs     # Atomic JSON persistence & corruption recovery
│   │   │   ├── DnsService.cs        # Adapter detection, netsh DNS apply/DHCP, flush & UDP test
│   │   │   └── SoundService.cs      # Local WAV generator & non-blocking audio player
│   │   └── UI/
│   │       ├── Theme.cs             # Dark gaming navy/purple & Light theme palette + GDI+ helpers
│   │       ├── IconRenderer.cs      # Vector icon & provider logo renderer + self-contained .ico
│   │       ├── TrayMenuRenderer.cs  # Custom dark context menu renderer for System Tray
│   │       ├── AddEditDnsDialog.cs  # Modal dialog for adding/editing IPv4 & IPv6 DNS profiles
│   │       └── MainForm.cs          # Main dashboard,Favorites, Settings, About & Tray controller
│   └── Installer/
│       └── InstallerProgram.cs      # Self-contained GUI & silent installer/uninstaller
├── tests/
│   └── TestRunner.cs                # 41-assertion automated QA & verification suite
├── build/
│   ├── DNSParty.exe                 # Compiled main executable
│   ├── DNSPartyTests.exe            # Compiled QA test runner
│   └── dashboard_preview.png        # Rendered dashboard verification capture
└── dist/
    ├── DNSParty-Portable.exe        # Standalone single-file portable executable (~343 KB)
    ├── DNSParty-Portable/           # Portable folder distribution (with portable.flag)
    ├── DNSParty-Portable-v1.0.0.zip # Zipped portable package
    └── DNSParty-Setup.exe           # Standalone Windows Setup Installer
```

---

## Installation & Usage

### Option 1: Portable Version (No Installation Required)
1. Run [`dist/DNSParty-Portable.exe`](file:///c:/Users/EMVI/Desktop/DNS%20Party/dist/DNSParty-Portable.exe) directly, or extract `dist/DNSParty-Portable-v1.0.0.zip` anywhere on your PC.
2. When `portable.flag` is present in the same folder as `DNSParty.exe` (as in `dist/DNSParty-Portable/`), all settings and custom DNS profiles are stored right next to the executable in `portable_config.json`.

### Option 2: Installer Version
1. Run [`dist/DNSParty-Setup.exe`](file:///c:/Users/EMVI/Desktop/DNS%20Party/dist/DNSParty-Setup.exe) (supports interactive GUI installation or silent install via `DNSParty-Setup.exe /S`).
2. Installs DNS Party to `%LocalAppData%\Programs\DNSParty`, creates Desktop and Start Menu shortcuts, and registers the uninstaller in Windows Installed Apps (`DNSParty-Setup.exe /U`).

---

## Building from Source

### Requirements
- Windows 10 or Windows 11 (.NET Framework 4.8 is pre-installed with Windows).
- MSBuild or Roslyn `csc.exe` (included with Visual Studio / Build Tools or `.NET Framework`).

### Build Commands
Run `build.bat` from the repository root:
```bat
.\build.bat
```
Or build directly with MSBuild:
```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe" "src\DNSParty\DNSParty.csproj" /p:Configuration=Release
```
