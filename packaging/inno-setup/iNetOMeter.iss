; Inno Setup Script for iNetOMeter
; Requires Inno Setup 6.0+ (ISCC.exe)

#define MyAppName "iNetOMeter"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "iNetOMeter Open Source Project"
#define MyAppURL "https://github.com/iNetOMeter/iNetOMeter"
#define MyAppExeName "iNetOMeter.exe"
#define SourceDir "..\..\publish\win-x64"

[Setup]
AppId={{5A8E4C31-2B14-4903-8B39-4E6D27318DF1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\dist
OutputBaseFilename=iNetOMeter-{#MyAppVersion}-win-x64-Setup
SetupIconFile=..\..\assets\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\assets\app.ico
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startwithwindows"; Description: "Start iNetOMeter when Windows starts"; GroupDescription: "Startup Options:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\QUICKSTART.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\assets\app.ico"; DestDir: "{app}\assets"; Flags: ignoreversion
Source: "..\..\assets\logo.svg"; DestDir: "{app}\assets"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\assets\app.ico"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\assets\app.ico"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\assets\app.ico"; WorkingDir: "{app}"; Tasks: startwithwindows

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up runtime generated caches if any, but preserve %LOCALAPPDATA%\iNetOMeter history/settings
Type: filesandordirs; Name: "{app}"
