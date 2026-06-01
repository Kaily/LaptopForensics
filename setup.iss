[Setup]
AppName=Laptop Forensics System
AppVersion=1.0
DefaultDirName={autopf}\Laptop Forensics System
DefaultGroupName=Laptop Forensics System
UninstallDisplayIcon={app}\laptop-forensics.exe
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=LaptopForensicsSetup
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "publish\laptop-forensics.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Laptop Forensics System"; Filename: "{app}\laptop-forensics.exe"
Name: "{autodesktop}\Laptop Forensics System"; Filename: "{app}\laptop-forensics.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"
