@echo off
setlocal
echo ============================================================
echo  Building DNS Party v1.0.0 (Release, Portable, Installer)
echo ============================================================

set "CSC=C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
if not exist "%CSC%" (
    set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
)

if not exist "build" mkdir "build"
if not exist "dist" mkdir "dist"

echo [1/3] Compiling DNSParty.exe...
"%CSC%" /nologo /target:winexe /optimize+ /out:"build\DNSParty.exe" ^
    /win32icon:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico" ^
    /win32manifest:"src\DNSParty\app.manifest" ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico",DNSParty.app.ico ^
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
    echo [ERROR] Compilation failed.
    exit /b 1
)

echo [2/3] Copying Portable executable, compiling Uninstaller, and building Installer...
copy /Y "build\DNSParty.exe" "dist\DNSParty-Portable.exe" >nul
copy /Y "build\DNSParty.exe" "dist\DNSParty-Portable\DNSParty.exe" >nul

"%CSC%" /nologo /target:winexe /optimize+ /out:"build\Unins000.exe" ^
    /win32icon:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico" ^
    /win32manifest:"src\DNSParty\app.manifest" ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    "src\Installer\UninstallerProgram.cs"

powershell -Command "if (Test-Path 'build\Payload.zip') { Remove-Item 'build\Payload.zip' }; Compress-Archive -Path 'build\DNSParty.exe', 'build\Unins000.exe' -DestinationPath 'build\Payload.zip' -Force"
powershell -Command "if (Test-Path 'dist\DNSParty-Portable.zip') { Remove-Item 'dist\DNSParty-Portable.zip' }; Compress-Archive -Path 'dist\DNSParty-Portable\*' -DestinationPath 'dist\DNSParty-Portable.zip' -Force"

"%CSC%" /nologo /target:winexe /optimize+ /out:"dist\DNSParty-Setup.exe" ^
    /win32icon:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico" ^
    /win32manifest:"src\DNSParty\app.manifest" ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Icons\app.ico",DNSParty.app.ico ^
    /resource:"assets\new_logos\DNS-Party-Logo-Pack\Installer\installer-logo-512.png",DNSParty.installer-logo-512.png ^
    /resource:"build\Payload.zip",DNSParty.Payload.zip ^
    /resource:"LICENSE",DNSParty.LICENSE ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll ^
    "src\Installer\InstallerProgram.cs" "src\DNSParty\VersionInfo.cs"

echo [3/3] Running Automated QA Verification Suite...
"%CSC%" /nologo /target:exe /optimize+ /out:"build\DNSPartyTests.exe" ^
    /r:"build\DNSParty.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    "tests\TestRunner.cs"
"build\DNSPartyTests.exe"

echo ============================================================
echo  Build Complete:
echo   - build\DNSParty.exe
echo   - dist\DNSParty-Portable.exe
echo   - dist\DNSParty-Portable.zip
echo   - dist\DNSParty-Setup.exe
echo ============================================================
