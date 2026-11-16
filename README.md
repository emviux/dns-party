# DNS Party — Windows DNS Manager

[![Persian Description](https://img.shields.io/badge/%F0%9F%87%AE%F0%9F%87%B7_%D9%81%D8%A7%D8%B1%D8%B3%DB%8C-Persian_Description-2ea44f?style=for-the-badge)](README.fa.md)
[![Latest Release](https://img.shields.io/github/v/release/emviux/dns-party?style=for-the-badge&label=Download&color=7c3aed)](https://github.com/emviux/dns-party/releases/latest)

**DNS Party** is a fast, lightweight, gaming-inspired native Windows desktop utility for managing, testing, and switching between IPv4 and IPv6 DNS server configurations with 1-click System Tray and In-Game Overlay integration.

---

## Download & Installation (3 Ways)

You can download and run **DNS Party** using whichever method suits you best:

### 1. Install Wizard (Recommended)
1. Download **[`DNSParty-Setup.exe`](https://github.com/emviux/dns-party/raw/main/dist/DNSParty-Setup.exe)** (or grab it from [**GitHub Releases**](https://github.com/emviux/dns-party/releases/latest)).
2. Run the setup wizard, choose your preferred location (it automatically creates a `DNS Party` folder), and complete the installation.
3. Creates optional Desktop and Start Menu shortcuts, preserves your settings across updates in `%LocalAppData%\DNSParty`, and registers a genuine uninstaller (`Unins000.exe`) in **Windows Settings → Installed Apps** and **Control Panel**.
   - *Silent install supported via:* `DNSParty-Setup.exe /S`

### 2. Portable Version (No Installation Required)
1. Download **[`DNSParty-Portable.exe`](https://github.com/emviux/dns-party/raw/main/dist/DNSParty-Portable.exe)** (or grab it from [**GitHub Releases**](https://github.com/emviux/dns-party/releases/latest)).
2. Run `DNSParty-Portable.exe` directly from any folder — no installation needed.
3. *Optional USB / Fully Portable Mode:* Place an empty file named `portable.flag` next to `DNSParty-Portable.exe` to save all settings and custom DNS profiles right next to the executable in `portable_config.json`.

### 3. Build from Source via `build.bat` (For Technical Users)
If you prefer to inspect the code and compile everything yourself from source:
1. Ensure you are on **Windows 10 or Windows 11** with **Visual Studio** (or **Visual Studio Build Tools**) installed with C# / MSBuild support.
2. Clone the repository and run `build.bat`:
```bat
git clone https://github.com/emviux/dns-party.git
cd dns-party
.\build.bat
```
3. `build.bat` automatically locates the Roslyn C# compiler (`csc.exe`), compiles the main app, dedicated uninstaller (`Unins000.exe`), and setup wizard into `dist\DNSParty-Portable.exe` and `dist\DNSParty-Setup.exe`, and executes the automated QA verification suite.

---

## Key Features

- **1-Click DNS Switching**: Quickly switch between **Cloudflare** (`1.1.1.1`), **Google DNS** (`8.8.8.8`), **Quad9** (`9.9.9.9`), **AdGuard DNS** (`94.140.14.14`), **OpenDNS** (`208.67.222.222`), and unlimited custom IPv4/IPv6 DNS profiles.
- **Real Windows Verification**: Reads actual Windows network adapter state (`NetworkInterface` + `HKLM\SYSTEM\CurrentControlSet\Services\Tcpip(6)\Parameters\Interfaces\{GUID}`) to distinguish requested vs. active DNS and never display a false connection state.
- **In-Game Overlay**: Transparent, borderless DNS switching overlay accessible via a global keyboard shortcut (default `Shift+Tab`, configurable) so you can switch DNS profiles without leaving your game.
- **Disable DNS (Restore Automatic DHCP)**: Restores the active Ethernet or Wi-Fi adapter back to Windows automatic DHCP DNS (`netsh interface ipv4/ipv6 set dnsservers ... source=dhcp`) and verifies restoration.
- **Flush DNS Cache**: Executes `ipconfig /flushdns` silently without flashing a command prompt window and confirms completion.
- **Direct UDP Port 53 DNS Latency Tester**: Measures real DNS server response times in milliseconds (`ms`) using RFC 1035 UDP DNS queries.
- **System Tray & Background Mode**:
  - Prompts on close whether to keep running in the background or exit completely (with an option to remember your choice).
  - Excludes itself from `Alt+Tab` (`WS_EX_TOOLWINDOW`) and consumes ~0% idle CPU while hidden in the System Tray.
  - Right-click tray menu provides instant DNS switching, active DNS checkmark, **User Favorites** submenu, **Disable DNS**, **Flush DNS Cache**, **Settings**, and **Exit**.
- **Auto-Updater & What's New Highlights**: Checks GitHub Releases for updates and displays a "What's New" summary dialog on first launch after an update.
- **Subtle Connection Chimes**: Plays short, locally generated PCM `.wav` audio effects (`connect.wav` / `disconnect.wav`) when DNS changes succeed (toggleable in Settings).
- **Smart UAC Privileges**: Runs normal UI as a standard user (`asInvoker`) so startup and browsing require no UAC prompt; seamlessly elevates (`--elevated-apply` / `--elevated-dhcp` via `runas`) only when modifying Windows adapter DNS settings.

---

## Project Structure

```text
DNS Party/
├── ARCHITECTURE.md                  # Architecture, stack benchmarks & design decisions
├── README.md                        # English documentation & installation guide
├── README.fa.md                     # Persian (RTL) documentation & installation guide
├── build.bat                        # 1-click source build & automated QA test script
├── assets/                          # Application icons, branding & tray icon pack
├── src/
│   ├── DNSParty/
│   │   ├── DNSParty.csproj          # MSBuild project file (.NET Framework 4.8)
│   │   ├── app.manifest             # PerMonitorV2 DPI & UAC manifest
│   │   ├── Program.cs               # Single-instance entry point & CLI elevation helper
│   │   ├── VersionInfo.cs           # Application version & repository metadata
│   │   ├── Models/
│   │   │   ├── DnsProfile.cs        # DNS profile model, defaults & strict IPv4/IPv6 validation
│   │   │   ├── AppSettings.cs       # User settings & profile persistence model
│   │   │   └── DnsStatusSnapshot.cs # Live Windows network adapter DNS state snapshot
│   │   ├── Services/
│   │   │   ├── ConfigService.cs     # Atomic JSON persistence & corruption recovery
│   │   │   ├── DnsService.cs        # Adapter detection, netsh DNS apply/DHCP, flush & UDP test
│   │   │   ├── HotkeyService.cs     # Global hotkey registration for In-Game Overlay
│   │   │   ├── SoundService.cs      # Local WAV generator & non-blocking audio player
│   │   │   └── UpdateService.cs     # GitHub Releases update checker & installer launcher
│   │   └── UI/
│   │       ├── Theme.cs             # Dark gaming navy/purple & Light theme palette + GDI+ helpers
│   │       ├── IconRenderer.cs      # Vector icon & provider logo renderer + multi-DPI tray icons
│   │       ├── TrayMenuRenderer.cs  # Custom dark context menu renderer for System Tray
│   │       ├── AddEditDnsDialog.cs  # Modal dialog for adding/editing IPv4 & IPv6 DNS profiles
│   │       ├── OverlayForm.cs       # Borderless In-Game DNS switching overlay
│   │       └── MainForm.cs          # Main dashboard, Favorites, Settings, About & Tray controller
│   └── Installer/
│       ├── InstallerProgram.cs      # Interactive GUI & silent Windows Setup Wizard
│       └── UninstallerProgram.cs    # Dedicated Windows Uninstaller (Unins000.exe)
├── tests/
│   └── TestRunner.cs                # Automated QA & verification suite
└── dist/
    ├── DNSParty-Setup.exe           # Method 1: Standalone Windows Install Wizard
    └── DNSParty-Portable.exe        # Method 2: Standalone Single-File Portable Executable
```

---

## License

Released under the [MIT License](LICENSE).
