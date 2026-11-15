import os
import re

file_path = r'src\Installer\InstallerProgram.cs'
with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('Text = "DNS Party v1.0.0 — Setup";', 'Text = $"DNS Party v{DNSParty.VersionInfo.CurrentVersion} — Setup";')
content = content.replace('Text = "Install DNS Party v1.0.0",', 'Text = $"Install DNS Party v{DNSParty.VersionInfo.CurrentVersion}",')
content = content.replace('"DNS Party v1.0.0 has been installed to:\\r\\n" + _installDir', '$"DNS Party v{DNSParty.VersionInfo.CurrentVersion} has been installed to:\\r\\n" + _installDir')

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)
