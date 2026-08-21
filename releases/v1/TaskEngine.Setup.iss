; Script do Inno Setup para o instalador do TaskEngine (V1).
;
; Gera um .exe tradicional (não MSIX): sem certificado, sem cerimônia de confiança manual.
; O pior caso na primeira execução é o SmartScreen do Windows mostrar um aviso genérico
; ("Windows protegeu seu PC") com um botão "Mais informações" -> "Executar assim mesmo" -
; não exige PowerShell nem importar certificado (ver releases/v1/INSTALL.md).
;
; Pré-requisito para compilar: pasta de publish gerada via
;   dotnet publish src/Frontend/TaskEngine.Desktop -f net10.0-windows10.0.19041.0 -c Release ^
;     -r win-x64 --self-contained true -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true ^
;     -o publish-out
; (pasta publish-out na raiz do repo, não versionada - ver .gitignore). WindowsAppSDKSelfContained=true
; embute os binários do Windows App Runtime na própria publicação, então a máquina de destino não
; precisa ter o Windows App Runtime pré-instalado.
;
; Compilar com:
;   "C:\Users\<usuario>\AppData\Local\Programs\Inno Setup 6\ISCC.exe" releases\v1\TaskEngine.Setup.iss

#define MyAppName "TaskEngine"
#define MyAppVersion "1.0.0.2"
#define MyAppPublisher "Arthur de Paula Correa"
#define MyPublishDir "..\..\publish-out"

[Setup]
AppId={{8F2C7B1E-6A3D-4C9E-9F2A-1D4E5B6C7A8F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
; Instala em {localappdata}, sem exigir privilégios de administrador (sem UAC) - prioriza
; "só clicar e instalar" em vez de instalação para todos os usuários.
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=.
OutputBaseFilename=TaskEngineSetup
SetupIconFile={#MyPublishDir}\appicon.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
UninstallDisplayIcon={app}\TaskEngine.Desktop.exe

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na Área de Trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\TaskEngine.Desktop.exe"; IconFilename: "{app}\appicon.ico"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\TaskEngine.Desktop.exe"; IconFilename: "{app}\appicon.ico"; Tasks: desktopicon

[Run]
; "/show" diz ao app pra abrir a janela principal imediatamente em vez de iniciar escondida na
; bandeja (comportamento normal de startup/autostart) - sem isso, o app só piscava e sumia,
; obrigando quem instalou a caçar o ícone na bandeja (área de notificação) pra abrir de fato.
Filename: "{app}\TaskEngine.Desktop.exe"; Parameters: "/show"; Description: "Abrir o {#MyAppName} agora"; Flags: nowait postinstall skipifsilent unchecked

[Code]
// O desinstalador padrão do Inno Setup só remove os arquivos do programa (seção [Files]) -
// nunca %LOCALAPPDATA%\TaskEngine (banco SQLite + configurações, gravados em runtime pelo app,
// fora do escopo do instalador). Sem isso, reinstalar depois de desinstalar faz o app aparecer
// "magicamente" já conectado ao provedor, pulando o onboarding, porque os dados antigos
// continuam lá.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  LocalDataDir: String;
  UserWantsDataDeleted: Boolean;
begin
  if CurUninstallStep <> usPostUninstall then
    exit;

  LocalDataDir := ExpandConstant('{localappdata}\TaskEngine');
  if not DirExists(LocalDataDir) then
    exit;

  // Decisão de padrão: em desinstalação silenciosa (/SILENT, /VERYSILENT - ex. usada por
  // automações/scripts de reinstalação), NÃO perguntamos e NÃO apagamos os dados locais por
  // padrão - travar numa caixa de diálogo quebraria a automação, e apagar dados do usuário sem
  // confirmação explícita é arriscado demais para ser o padrão silencioso. Só em desinstalação
  // interativa perguntamos, com "Não" como resposta padrão (botão focado), já que apagar é
  // irreversível.
  if UninstallSilent() then
    exit;

  UserWantsDataDeleted := SuppressibleMsgBox(
    'Deseja também apagar os dados locais do TaskEngine (tarefas, histórico, configurações)?' + #13#10 + #13#10 +
    'Esta ação não pode ser desfeita.',
    mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES;

  if UserWantsDataDeleted then
    DelTree(LocalDataDir, True, True, True);
end;
