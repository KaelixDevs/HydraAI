#ifndef PublishDir
  #define PublishDir "..\artifacts\portable"
#endif
#ifndef OutputPath
  #define OutputPath "..\artifacts\installer"
#endif

[Setup]
AppId={{3DBEC5BE-4D1A-4735-B4DD-18FB73E5FB49}
AppName=Hydra AI
AppVersion=0.1.1
AppVerName=Hydra AI 0.1.1 Development
AppPublisher=Hydra AI contributors
DefaultDirName={localappdata}\Programs\HydraAI
DefaultGroupName=Hydra AI
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputPath}
OutputBaseFilename=HydraAI-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\HydraAI.exe
LicenseFile=..\LICENSE
CloseApplications=yes
RestartApplications=no
VersionInfoVersion=0.1.1.0
VersionInfoDescription=Hydra AI local-first platform

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD_PARTY.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Hydra AI"; Filename: "{app}\HydraAI.exe"
Name: "{group}\Uninstall Hydra AI"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Hydra AI"; Filename: "{app}\HydraAI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\HydraAI.exe"; Description: "Launch Hydra AI"; Flags: nowait postinstall skipifsilent

; User data lives outside {app} and is deliberately retained on uninstall.
; Running this installer again replaces application files for repair/update.
; Production releases must Authenticode-sign the application and setup executable.
