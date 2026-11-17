@echo off
setlocal
echo ============================================================
echo  Building DNS Party v1.2.1 (Portable + Setup Installer)
echo ============================================================

set "CSC="
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" goto :fallback_csc
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\Roslyn\csc.exe -latest`) do set "CSC=%%i"

:fallback_csc
if not defined CSC if exist "C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe" set "CSC=C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
if not defined CSC set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo Using compiler: "%CSC%"

if not exist "build" mkdir "build"
if not exist "dist" mkdir "dist"

echo [1/3] Compiling DNSParty.exe...
"%CSC%" /nologo /target:winexe /optimize+ /out:"build\DNSParty.exe" ^
    /win32icon:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico" ^
    /win32manifest:"src\DNSParty\app.manifest" ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico",DNSParty.app.ico ^
    /resource:"assets\providers\Electro.png",DNSParty.Electro.png ^
    /resource:"assets\providers\403_Online.png",DNSParty.403_Online.png ^
    /resource:"assets\providers\Radar_Game.png",DNSParty.Radar_Game.png ^
    /resource:"assets\providers\Shecan.png",DNSParty.Shecan.png ^
    /resource:"assets\providers\Begzar.png",DNSParty.Begzar.png ^
    /resource:"assets\providers\Shatel.png",DNSParty.Shatel.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Branding\logo-transparent.png",DNSParty.logo-transparent.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Overlay\overlay-badge-256.png",DNSParty.overlay-badge-256.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Installer\installer-logo-512.png",DNSParty.installer-logo-512.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-default-16.png",DNSParty.tray-default-16.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-default-20.png",DNSParty.tray-default-20.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-default-24.png",DNSParty.tray-default-24.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-default-32.png",DNSParty.tray-default-32.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-connected-16.png",DNSParty.tray-connected-16.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-connected-20.png",DNSParty.tray-connected-20.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-connected-24.png",DNSParty.tray-connected-24.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-connected-32.png",DNSParty.tray-connected-32.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-changing-16.png",DNSParty.tray-changing-16.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-changing-24.png",DNSParty.tray-changing-24.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-changing-32.png",DNSParty.tray-changing-32.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-error-16.png",DNSParty.tray-error-16.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-error-24.png",DNSParty.tray-error-24.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Tray\tray-error-32.png",DNSParty.tray-error-32.png ^
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Net.Http.dll ^
    "src\DNSParty\VersionInfo.cs" "src\DNSParty\Models\*.cs" "src\DNSParty\Services\*.cs" "src\DNSParty\UI\*.cs" "src\DNSParty\Program.cs"

if errorlevel 1 (
    echo [ERROR] DNSParty.exe compilation failed.
    exit /b 1
)

echo [2/3] Creating Portable executable, Uninstaller, and Setup Wizard...
copy /Y "build\DNSParty.exe" "dist\DNSParty-Portable.exe" >nul
if errorlevel 1 (
    echo [ERROR] Could not copy dist\DNSParty-Portable.exe ^(make sure it is not currently running^).
    exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /out:"build\Unins000.exe" ^
    /win32icon:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico" ^
    /win32manifest:"src\DNSParty\app.manifest" ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    "src\Installer\UninstallerProgram.cs"

if errorlevel 1 (
    echo [ERROR] Uninstaller compilation failed.
    exit /b 1
)

powershell -NoProfile -Command "if (Test-Path 'build\Payload.zip') { Remove-Item 'build\Payload.zip' -Force }; Compress-Archive -Path 'build\DNSParty.exe', 'build\Unins000.exe' -DestinationPath 'build\Payload.zip' -Force"

"%CSC%" /nologo /target:winexe /optimize+ /out:"dist\DNSParty-Setup.exe" ^
    /win32icon:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico" ^
    /win32manifest:"src\DNSParty\app.manifest" ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico",DNSParty.app.ico ^
    /resource:"assets\providers\Electro.png",DNSParty.Electro.png ^
    /resource:"assets\providers\403_Online.png",DNSParty.403_Online.png ^
    /resource:"assets\providers\Radar_Game.png",DNSParty.Radar_Game.png ^
    /resource:"assets\providers\Shecan.png",DNSParty.Shecan.png ^
    /resource:"assets\providers\Begzar.png",DNSParty.Begzar.png ^
    /resource:"assets\providers\Shatel.png",DNSParty.Shatel.png ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Installer\installer-logo-512.png",DNSParty.installer-logo-512.png ^
    /resource:"build\Payload.zip",DNSParty.Payload.zip ^
    /resource:"LICENSE",DNSParty.LICENSE ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
    "src\Installer\InstallerProgram.cs" "src\DNSParty\VersionInfo.cs"

if errorlevel 1 (
    echo [ERROR] Setup Wizard compilation failed.
    exit /b 1
)

if /I "%~1"=="--skip-tests" goto :done

echo [3/3] Running Automated QA Verification Suite...
"%CSC%" /nologo /target:exe /optimize+ /out:"build\DNSPartyTests.exe" ^
    /r:"build\DNSParty.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    "tests\TestRunner.cs"
if errorlevel 1 (
    echo [ERROR] TestRunner compilation failed.
    exit /b 1
)
"build\DNSPartyTests.exe"
if errorlevel 1 (
    echo [ERROR] Automated QA tests failed.
    exit /b 1
)

:done
echo ============================================================
echo  Build Complete:
echo   - dist\DNSParty-Setup.exe     (Install Wizard)
echo   - dist\DNSParty-Portable.exe  (Portable Executable)
echo ============================================================




