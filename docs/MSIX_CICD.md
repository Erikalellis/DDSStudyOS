# MSIX e CI/CD (Release Guide)

## Estado atual
O projeto está em modo desktop WinUI 3 com geração de executável (`publish`) validada.

Comando recomendado (build + publish via Visual Studio MSBuild):
```powershell
.\scripts\build-release.ps1
```

Observação importante:
- `dotnet build` pode falhar em projetos WinUI 3 sem os componentes Appx/MSBuild do Visual Studio.
- O `publish` recria `DDSStudyOS.App.exe`; portanto, assine novamente após cada publish.

## Pré-checklist antes do MSIX
- [x] Branding e nome do produto (`DDS StudyOS`)
- [x] Versionamento de assembly
- [x] Diagnóstico técnico no app (Configurações)
- [x] Exportação de bundle de diagnóstico `.zip`
- [x] Backup criptografado com validação
- [x] Build/Publish Release sem erros

## Próximo passo para MSIX
1. Adicionar projeto de empacotamento (`Windows Application Packaging Project`) na solution.
2. Definir identidade final no `Package.appxmanifest`:
- `Identity Name`
- `Publisher`
- `Version`
3. Configurar ícones e assets de loja.
4. Assinar pacote com certificado:
- Teste: certificado local temporário
- Produção: certificado de code signing oficial

## Assinatura
Sem certificado válido, o MSIX não terá fluxo de instalação confiável em produção.

Recomendação:
1. Usar certificado de teste apenas para homologação interna.
2. Usar certificado oficial antes de distribuição externa.

### Assinatura interna (self-signed)
Scripts adicionados:
- `scripts/build-release.ps1`: build + publish com descoberta automática do `MSBuild.exe`
- `scripts/sign-release.ps1`: assina `.exe/.msix` via `.pfx` ou via thumbprint no store
- `scripts/install-internal-cert.ps1`: instala `.cer` no `TrustedPublisher` e `Root` do usuário
- `scripts/Instalar_DDS.bat`: launcher para instalação em `LocalMachine` com elevação UAC

Exemplo de uso:
```powershell
.\scripts\install-internal-cert.ps1 -CerPath "C:\Users\robso\Desktop\DDS_Studios_Final.cer"
.\scripts\sign-release.ps1 -PfxPath "C:\Users\robso\Desktop\DDS_Studios_Final.pfx" -PfxPassword "<SENHA>"
```

Exemplo sem exportar `.pfx` (assina com certificado instalado no store):
```powershell
.\scripts\sign-release.ps1 -CertThumbprint "6780CE530A33615B591727F5334B3DD075B76422" -CertStoreScope CurrentUser
```

Exemplo robusto com validação de thumbprint:
```powershell
.\scripts\install-internal-cert.ps1 `
  -CerPath "C:\Users\robso\Desktop\DDS_Studios_Final.cer" `
  -ExpectedThumbprint "6780CE530A33615B591727F5334B3DD075B76422" `
  -StoreScope LocalMachine `
  -InstallTrustedPublisher $true `
  -InstallRoot $true
```

### Inno Setup (snippet de execução)
Use no instalador para injetar o certificado ao final:
```ini
[Files]
Source: "scripts\install-internal-cert.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "scripts\DDS_Studios_Final.cer"; DestDir: "{app}"; Flags: ignoreversion

[Run]
Filename: "powershell.exe"; \
Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\install-internal-cert.ps1"" -CerPath ""{app}\DDS_Studios_Final.cer"" -ExpectedThumbprint ""6780CE530A33615B591727F5334B3DD075B76422"" -StoreScope LocalMachine -InstallTrustedPublisher $true -InstallRoot $true"; \
Flags: runascurrentuser waituntilterminated
```

## CI/CD (GitHub Actions) - estrutura sugerida
- `restore`
- `build release`
- `publish`
- `package msix`
- `sign`
- `upload artifact`
