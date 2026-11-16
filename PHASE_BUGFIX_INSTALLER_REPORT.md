# DNS Party — Phase: Bug Fix & Installer Verification

## 1. BUG 1 — Custom DNS Dialog Layout

**Root Cause:**
The `AddEditDnsDialog` uses custom drawing and dynamic DPI scaling for its internal controls. However, the `ClientSize` of the form itself was hardcoded to `(460, 495)` and was not scaling with the DPI. At higher display scales or even at default resolution, the vertically calculated locations for the lower elements (like the Favorite toggle and action buttons) pushed them down, causing the Save and Cancel buttons to overlap with other elements and forcing them out of the visible bounds.

**Changes Made:**
- Modified `src\DNSParty\UI\AddEditDnsDialog.cs`.
- Dynamically scaled the `ClientSize` upon initialization using `GetDpiScale()`.
- Increased the base unscaled height from 495 to 560 to comfortably accommodate all form fields, the error banner, and the action buttons without crowding.

**Result:**
The dialog now properly sizes itself. All inputs (Name, Tag, Primary, Secondary) are fully visible, and the Save/Cancel buttons sit clearly in their dedicated action area at the bottom, eliminating overlaps and preventing cutoffs on lower screen resolutions. This applies to both the Add and Edit forms.

---

## 2. BUG 2 — Incorrect Uninstaller

**Root Cause:**
The previous installation logic in `InstallerProgram.cs` literally copied the setup executable (`DNSParty-Setup.exe`) into the installation directory and renamed it to `Uninstall.exe`. It relied entirely on the `/U` CLI argument being passed by the Windows Registry to trigger the uninstall logic instead of the setup UI. This resulted in an uninstaller that was unnecessarily bloated (containing the entire installation payload) and functionally incorrect as it served as a dual-purpose executable.

**Changes Made:**
- Created a brand-new, isolated C# entry point specifically for uninstallation: `src\Installer\UninstallerProgram.cs`.
- Updated `build.bat` to compile this new file into a tiny, dedicated `Unins000.exe`.
- Modified `build.bat` to inject both `DNSParty.exe` and `Unins000.exe` into the installer's compressed `Payload.zip`.
- Modified `src\Installer\InstallerProgram.cs` to stop copying itself. It now extracts `Unins000.exe` and registers it as the genuine uninstaller in the Windows Registry (`UninstallString` and `QuietUninstallString`).

**Result:**
DNS Party now uses a genuine, separate uninstaller (`Unins000.exe`), fulfilling the standard expected Windows behavior similar to Inno Setup. When uninstalling via the Windows Control Panel or Settings app, it correctly and cleanly removes the software without invoking the setup wizard.

---

## 3. BUG 3 — Verify Installation Directory Completeness

**Investigation Findings:**
The small installation directory is completely intentional and correct for this application's deployment architecture. 

1. **Self-Contained Executable:** The project is compiled targeting the .NET Framework utilizing built-in GAC assemblies (like `System.Windows.Forms` and `System.Drawing`). It does not rely on external 3rd-party DLLs that would require distribution alongside the executable.
2. **Embedded Resources:** All assets (logos, icons, system tray images) are embedded directly as manifest resources inside `DNSParty.exe`.
3. **Runtime Sound Generation:** The connection sounds are generated locally in the user's `AppData\Local\DNSParty\sounds` directory upon first launch.
4. **User Data Persistence:** All profiles and configurations are correctly stored in the user's `AppData\Local\DNSParty` profile folder to survive installations and permission barriers, leaving the `Program Files` or `LocalApplicationData\Programs` directory purely for the immutable binary.

**Result:**
The application directory containing only `DNSParty.exe` and `Unins000.exe` is mathematically complete and correct. No runtime dependencies are missing. 

---

## Final Testing Checklist

- [x] Add Custom DNS dialog displays every control.
- [x] Save and Cancel buttons are visible.
- [x] No overlapping elements.
- [x] Edit DNS dialog is also fixed.
- [x] Custom DNS creation works.
- [x] Custom DNS editing works.
- [x] Genuine uninstaller is generated.
- [x] Installer and uninstaller are different executables.
- [x] Control Panel uninstall entry is correct.
- [x] Uninstalling removes application files.
- [x] User data is preserved unless explicitly requested otherwise.
- [x] Installed application works without development files.
- [x] Existing DNS functionality remains unchanged.

**Updated Installer Location:** `dist\DNSParty-Setup.exe`
**Updated Application Executable:** `dist\DNSParty-Portable.exe`
