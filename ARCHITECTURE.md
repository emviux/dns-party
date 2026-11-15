# DNS Party — Architecture & Technical Design

**DNS Party** is a lightweight, high-performance Windows desktop application for switching and managing system DNS configurations with one click. It is engineered for ultra-low memory and CPU consumption, sub-second cold startup, native Windows 10/11 integration, and reliable real-world network verification.

---

## 1. Technology Stack Evaluation & Rationale

### Selected Stack: C# (Roslyn 5.11) + .NET Framework 4.8 + Custom-Rendered WinForms (GDI+ / DWM / Win32 P/Invoke)

During architecture evaluation, we compared candidate desktop frameworks on Windows 10 and Windows 11 across footprint, startup latency, binary size, runtime dependencies, and rendering control:

| Criterion | **C# / .NET 4.8 + Custom GDI+/DWM (Selected)** | Electron / Tauri (WebView2) | WPF (.NET 4.8 / .NET 8) | WinUI 3 / Windows App SDK |
| :--- | :--- | :--- | :--- | :--- |
| **Idle RAM Footprint** | **~12 – 22 MB** | 120 – 350+ MB | 45 – 85 MB | 60 – 110 MB |
| **Background / Tray RAM** | **< 10 MB** | 80 – 200 MB | 35 – 60 MB | 45 – 80 MB |
| **Cold Startup Time** | **< 150 ms (Instant)** | 800 – 2500 ms | 350 – 700 ms | 400 – 900 ms |
| **Executable / Dist Size** | **< 1.5 MB standalone** | 80 – 180 MB (or WebView2 dep) | 2 – 65 MB | 30 – 120 MB |
| **OS Pre-Installed Runtime** | **100% Built-in on Win 10 & 11** | No (requires Chromium/WebView2) | Yes (.NET 4.8) / No (.NET 8) | No (requires Windows App SDK) |
| **Idle CPU Usage** | **0.00%** (strictly event-driven) | 0.1 – 1.5% (background IPC/timers) | 0.00 – 0.2% | 0.00 – 0.3% |

### Why This Stack Wins for DNS Party
1. **Zero External Dependencies**: `.NET Framework 4.8` is pre-installed as an OS component on every Windows 10 (1903+) and Windows 11 installation. The compiled `DNSParty.exe` runs out-of-the-box without requiring users to download runtimes or framework installers.
2. **Compiler Availability**: Compiled directly using the modern **Roslyn C# compiler (`csc.exe` 5.11.0)** and **MSBuild 18.11** located in `C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin`.
3. **Pixel-Perfect Custom GDI+ & DWM Rendering**: By using a custom double-buffered (`OptimizedDoubleBuffer | AllPaintingInWmPaint | UserPaint | ResizeRedraw`) renderer over `System.Windows.Forms` paired with `dwmapi.dll` (`DwmSetWindowAttribute` for Dark Mode window frame and rounded corners on Windows 11), DNS Party achieves a modern, crisp, dark-first desktop UI without the layout overhead of XAML or a browser engine.
4. **Native System Integration**: Direct access to `System.Net.NetworkInformation`, `NotifyIcon` for rock-solid system tray persistence, `Microsoft.Win32.SystemEvents` for sleep/resume/network events, and Win32 APIs (`user32.dll`, `dwmapi.dll`, `shell32.dll`) for single-instance enforcement and UAC elevation.

---

## 2. Project Directory Structure

```text
c:\Users\EMVI\Desktop\DNS Party\
├── ARCHITECTURE.md                 # System architecture, contracts, and engineering standards
├── README.md                       # Build, packaging, and usage documentation
├── assets\                         # Extracted brand icons, provider badges, and .ico files
├── src\
│   └── DNSParty\
│       ├── Program.cs              # Entry point, single-instance Mutex + IPC wakeup, CLI elevated runner
│       ├── app.manifest            # PerMonitorV2 DPI awareness, Windows 10/11 compatibility
│       ├── Models\
│       │   ├── DnsProfile.cs       # DNS profile model, IPv4/IPv6 validator, and default providers
│       │   ├── AppSettings.cs      # Persisted user settings and state tracking model
│       │   └── DnsStatusSnapshot.cs# Real-time verified adapter and DNS state snapshot
│       ├── Services\
│       │   ├── DnsService.cs       # Adapter detection, DNS apply/reset, verification, elevated CLI bridge
│       │   ├── SettingsService.cs  # Atomic JSON persistence in %AppData%\DNSParty, startup registry
│       │   └── SoundService.cs     # Lightweight asynchronous feedback audio
│       └── UI\
│           ├── Theme.cs            # Dark & Light theme palettes, typography, DPI scaling helpers
│           ├── BrandAssets.cs      # Cached icon/bitmap loader and high-DPI vector fallback renderer
│           ├── MainForm.cs         # Custom-drawn 3-card dashboard, header, footer, tray integration
│           ├── CustomDnsDialog.cs  # Modal dialog to add/edit custom IPv4 & IPv6 DNS profiles
│           └── SettingsDialog.cs   # Modal dialog for General, DNS, Appearance, Sound, Notifications
├── tests\
│   └── DNSParty.Tests\             # Automated unit & verification suite for validation, persistence, DNS
├── build\                          # Build scripts (build.ps1) and intermediate compiler outputs
└── dist\                           # Final packaged DNSParty.exe release and portable bundle
```

---

## 3. DNS Management & Verification Strategy

### 3.1. Three-Layer State Model (No Fake Status)
Many naive DNS switchers immediately set their UI to "Connected" as soon as a user clicks a button, even if the command failed or another adapter overrode it. **DNS Party** enforces strict separation between three distinct states recorded in `DnsStatusSnapshot`:
1. **Requested Profile (`RequestedProfileId`)**: The profile ID the user most recently intended to activate.
2. **Applied Operation**: The execution result of the underlying `netsh` / network configuration command.
3. **Actual Windows-Detected DNS (`ActualPrimaryDns`, `ActualSecondaryDns`, `IsDhcpDns`, `ActiveProfileId`)**: Read directly from the live Windows network stack via `NetworkInterface.GetAllNetworkInterfaces()` and registry/interface inspection (`Tcpip\Parameters\Interfaces\{GUID}`).
   - The UI hero toggle and "ACTIVE" badges reflect **Actual Verified State**.
   - If the active adapter's actual primary DNS matches one of the configured profiles (e.g., `1.1.1.1` -> Cloudflare), `ActiveProfileId` and `ActiveProfileName` resolve to that profile.
   - If the adapter uses DHCP-assigned DNS (`NameServer` registry value is empty), `IsDhcpDns = true` and DNS Party displays as **Disabled (Automatic DHCP)**.
   - If the adapter has custom static DNS configured outside DNS Party's saved list, it accurately reports the custom IP address rather than falsely claiming DHCP or a preset profile.

### 3.2. Smart Active Adapter Detection
When `AutoDetectAdapter` is enabled (or `SelectedAdapterId` is empty):
- Enumerates `NetworkInterface.GetAllNetworkInterfaces()`.
- Filters out Loopback, Tunnel, and virtual/disconnected adapters (`OperationalStatus.Up`).
- Scores candidate Ethernet and Wireless (`Wireless80211`, `Ethernet`, `GigabitEthernet`) interfaces that possess a valid non-APIPA IPv4/IPv6 gateway (`GetIPProperties().GatewayAddresses.Count > 0`), prioritizing lowest IPv4 interface metric.
- Users can also explicitly pin a specific network adapter in **Settings -> DNS**.

### 3.3. IPv4 and IPv6 Support & Clean Reset
- **Applying a Profile**:
  - Determines address family via `DnsProfile.IsIPv6`.
  - Runs `netsh interface ipv4 set dnsservers name="<AdapterName>" static <PrimaryDns> primary validate=no` (or `ipv6` equivalent).
  - If `SecondaryDns` is provided, runs `netsh interface ipv4 add dnsservers name="<AdapterName>" <SecondaryDns> index=2 validate=no`.
  - Always executes `ipconfig /flushdns` immediately after applying or resetting DNS so the OS resolver cache never serves stale records.
- **Disabling / Resetting to DHCP**:
  - Runs `netsh interface ipv4 set dnsservers name="<AdapterName>" source=dhcp` (and `ipv6` if applicable), followed by `ipconfig /flushdns`.
  - Verifies via `NetworkInterface` and interface registry key (`SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{AdapterId}`) that static `NameServer` entries are cleared.

### 3.4. Concurrency Control & Event-Driven Updates
- **Operation Lock**: `DnsService` guards all apply/disable/refresh mutations with an asynchronous `SemaphoreSlim(1, 1)` lock and exposes an `IsBusy` state so rapid clicks on the hero toggle or provider cards never interleave `netsh` calls or corrupt adapter state.
- **Zero-Polling Event Architecture**:
  - Subscribes to `NetworkChange.NetworkAddressChanged` and `NetworkChange.NetworkAvailabilityChanged` (with a 350ms debounce timer to coalesce burst OS notifications).
  - Subscribes to `SystemEvents.PowerModeChanged` to automatically re-verify DNS status when Windows resumes from Sleep or Hibernate.
  - Zero background busy loops or periodic polling timers when idle.

---

## 4. UAC Elevation & Privilege Strategy

Changing network adapter DNS settings on Windows requires Administrator privileges (`TOKEN_ELEVATION`), whereas standard desktop utility usage (launching at Windows startup, sitting in the system tray, inspecting current DNS status, editing custom profiles, or toggling UI settings) does **not**.

### Seamless Dual-Mode Execution
1. **Standard-User UI Mode (Default)**:
   - `app.manifest` specifies `<requestedExecutionLevel level="asInvoker" uiAccess="false" />`.
   - This allows **Start with Windows** (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`) to work seamlessly without Windows blocking startup due to UAC requirements, and allows instant launch without an intrusive prompt just to check DNS status.
2. **On-Demand Elevated Helper Mode (or Full Admin Mode)**:
   - Before modifying adapter DNS, `DnsService` checks `IsRunningAsAdmin()` via `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`.
   - **If already elevated**: executes `netsh` and `ipconfig /flushdns` directly in-process with `CreateNoWindow = true`.
   - **If not elevated**: spawns a headless child instance of itself (`DNSParty.exe`) with `UseShellExecute = true`, `Verb = "runas"`, `WindowStyle = ProcessWindowStyle.Hidden`, passing structured CLI arguments:
     - `DNSParty.exe --elevated-apply "<AdapterName>" "<PrimaryIp>" "<SecondaryIp>" <ipv4|ipv6>`
     - `DNSParty.exe --elevated-dhcp "<AdapterName>"`
   - `Program.Main` intercepts `--elevated-*` flags before initializing WinForms, runs the privileged network commands + `ipconfig /flushdns`, and exits with code `0` (success) or non-zero error code.
   - If the user cancels the UAC prompt (`Win32Exception` error code `1223`), `DnsService` catches it cleanly, preserves the true UI state, and displays a clear status message (`"Administrator permission was canceled"`).
   - After the elevated process exits, `DnsService` refreshes the live `DnsStatusSnapshot` from Windows and confirms the actual DNS servers match the requested profile.

---

## 5. Core Models & Contracts

All shared models live under the `DNSParty.Models` namespace (`src/DNSParty/Models/`):

- **`DnsProfile`** (`src/DNSParty/Models/DnsProfile.cs`): Represents a built-in or user-created DNS provider (`Id`, `Name`, `Tag`, `PrimaryDns`, `SecondaryDns`, `IsIPv6`, `IsFavorite`, `IsDefaultProvider`, `IconKey`), includes strict IPv4/IPv6 validation (`ValidateDnsAddress`), and supplies the 5 default providers (`GetDefaultProfiles()`).
- **`AppSettings`** (`src/DNSParty/Models/AppSettings.cs`): Represents persisted application configuration and user preferences across General, DNS, Appearance, Sound, Notifications, and custom profiles.
- **`DnsStatusSnapshot`** (`src/DNSParty/Models/DnsStatusSnapshot.cs`): Represents an immutable point-in-time snapshot of the target network adapter and its verified DNS configuration.
