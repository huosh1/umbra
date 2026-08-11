#define MyAppName "Umbra"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define MyAppPublisher "zixload"
#define MyAppUrl "https://github.com/zixload/umbra"
#define MyAppExeName "Umbra.exe"
#define NativeHostName "com.umbra.browser_blocker"
#define StoreExtensionId "kihnnccjkhgjagaoljepcpghmfdpicoc"
#define ManualExtensionId "ijgalicomdmmcjecigefpchbdeiadnld"

[Setup]
AppId={{9D559CAA-3786-44F0-92E4-27D289230FAE}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
DefaultDirName={localappdata}\Programs\Umbra
DefaultGroupName=Umbra
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=Umbra-Setup-{#AppVersion}-x64
SetupIconFile=..\Umbra.App\icon.ico
UninstallDisplayIcon={app}\Umbra.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
CloseApplications=yes
CloseApplicationsFilter=Umbra.exe,Umbra.BrowserHost.exe
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Umbra installer
VersionInfoProductName=Umbra

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Umbra"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Umbra"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\{#NativeHostName}"; ValueType: string; ValueName: ""; ValueData: "{userappdata}\UmbraNative\data\browser-native-host.json"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Vivaldi\NativeMessagingHosts\{#NativeHostName}"; ValueType: string; ValueName: ""; ValueData: "{userappdata}\UmbraNative\data\browser-native-host.json"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Edge\NativeMessagingHosts\{#NativeHostName}"; ValueType: string; ValueName: ""; ValueData: "{userappdata}\UmbraNative\data\browser-native-host.json"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\{#NativeHostName}"; ValueType: string; ValueName: ""; ValueData: "{userappdata}\UmbraNative\data\browser-native-host.json"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Umbra}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#MyAppExeName}"; Parameters: "--updated"; Flags: nowait skipifdoesntexist; Check: IsUpdateMode

[UninstallDelete]
Type: files; Name: "{userappdata}\UmbraNative\data\browser-native-host.json"

[Code]
function IsUpdateMode: Boolean;
begin
  Result := CompareText(ExpandConstant('{param:UPDATE|0}'), '1') = 0;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  DataDirectory: String;
  WatchdogPidPath: String;
  WatchdogStoppedPath: String;
  Attempt: Integer;
begin
  { New BrowserHost versions observe this marker and close their own pipe.
    The Restart Manager configured above remains the compatibility path for
    1.0.3, whose host predates the cooperative shutdown protocol. }
  DataDirectory := ExpandConstant('{userappdata}\UmbraNative\data');
  ForceDirectories(DataDirectory);
  SaveStringToFile(DataDirectory + '\browser-host.stop-request',
    GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':'), False);

  { A manual repair/update does not pass through App.CheckForUpdatesAsync,
    so it must also stop the elevated watchdog before replacing the shared
    self-contained .NET runtime files (for example clrjit.dll). }
  WatchdogPidPath := DataDirectory + '\watchdog.pid';
  WatchdogStoppedPath := DataDirectory + '\watchdog.stopped';
  if FileExists(WatchdogPidPath) then
  begin
    DeleteFile(WatchdogStoppedPath);
    SaveStringToFile(DataDirectory + '\watchdog.stop-request',
      GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':'), False);
    for Attempt := 1 to 50 do
    begin
      if FileExists(WatchdogStoppedPath) or (not FileExists(WatchdogPidPath)) then
        Break;
      Sleep(200);
    end;
  end;
  Sleep(1000);
  Result := '';
end;

function JsonEscape(Value: String): String;
begin
  StringChangeEx(Value, '\', '\\', True);
  StringChangeEx(Value, '"', '\"', True);
  Result := Value;
end;
procedure WriteNativeHostManifest;
var
  DataDirectory: String;
  ManifestPath: String;
  HostPath: String;
  Manifest: String;
begin
  DataDirectory := ExpandConstant('{userappdata}\UmbraNative\data');
  ManifestPath := DataDirectory + '\browser-native-host.json';
  HostPath := ExpandConstant('{app}\Umbra.BrowserHost.exe');
  ForceDirectories(DataDirectory);
  Manifest := '{"name":"{#NativeHostName}","description":"Umbra browser blocking host","path":"' +
    JsonEscape(HostPath) + '","type":"stdio","allowed_origins":["chrome-extension://{#StoreExtensionId}/","chrome-extension://{#ManualExtensionId}/"]}';
  SaveStringToFile(ManifestPath, Manifest, False);
end;

procedure ClearBrowserHostStopRequest;
begin
  DeleteFile(ExpandConstant('{userappdata}\UmbraNative\data\browser-host.stop-request'));
end;

procedure ClearWatchdogStopSignals;
begin
  DeleteFile(ExpandConstant('{userappdata}\UmbraNative\data\watchdog.stop-request'));
  DeleteFile(ExpandConstant('{userappdata}\UmbraNative\data\watchdog.stopped'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    ClearBrowserHostStopRequest;
    ClearWatchdogStopSignals;
    WriteNativeHostManifest;
  end;
end;

procedure DeinitializeSetup;
begin
  { Never leave browser integration paused after a cancelled or failed setup. }
  ClearBrowserHostStopRequest;
  ClearWatchdogStopSignals;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM Umbra.exe /F', '', SW_HIDE,
      ewWaitUntilTerminated, ResultCode);
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Umbra');
  end;
end;
