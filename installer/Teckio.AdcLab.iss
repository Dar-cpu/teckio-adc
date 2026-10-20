; Build with Inno Setup 6: ISCC /DAppVersion=1.0.1 Teckio.AdcLab.iss
#ifndef AppVersion
  #error AppVersion must match Directory.Build.props
#endif

[Setup]
AppId={{9D31147D-E927-4B9D-8DE1-6A19DADC7195}
AppName=TECKIO ADC Lab
AppVersion={#AppVersion}
AppPublisher=TECKIO
AppPublisherURL=https://github.com/Dar-cpu/teckio-adc
AppSupportURL=https://github.com/Dar-cpu/teckio-adc/issues
AppUpdatesURL=https://github.com/Dar-cpu/teckio-adc/releases/latest
DefaultDirName={localappdata}\Programs\TECKIO\ADC Lab
DefaultGroupName=TECKIO ADC Lab
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts\installer
OutputBaseFilename=Teckio.AdcLab.Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Teckio.AdcLab.exe
CloseApplications=yes
CloseApplicationsFilter=Teckio.AdcLab.exe
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "..\artifacts\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\TECKIO ADC Lab"; Filename: "{app}\Teckio.AdcLab.exe"
Name: "{autodesktop}\TECKIO ADC Lab"; Filename: "{app}\Teckio.AdcLab.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Teckio.AdcLab.exe"; Description: "Abrir TECKIO ADC Lab"; Flags: nowait postinstall skipifsilent

; No [UninstallDelete] rule: preferences and user captures must survive uninstall.
