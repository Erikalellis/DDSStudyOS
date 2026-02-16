@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "PS_SCRIPT=%SCRIPT_DIR%install-internal-cert.ps1"
set "CER_FILE=%SCRIPT_DIR%DDS_Studios_Final.cer"
set "EXPECTED_THUMBPRINT=6780CE530A33615B591727F5334B3DD075B76422"

if not "%~1"=="" (
    set "CER_FILE=%~1"
)

if not exist "%PS_SCRIPT%" (
    echo [ERRO] Script nao encontrado: %PS_SCRIPT%
    exit /b 1
)

if not exist "%CER_FILE%" (
    echo [ERRO] Certificado nao encontrado: %CER_FILE%
    echo Dica: copie o arquivo DDS_Studios_Final.cer para esta pasta ou passe o caminho como parametro.
    exit /b 1
)

echo Instalando certificado DDS StudyOS...
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" ^
    -CerPath "%CER_FILE%" ^
    -ExpectedThumbprint "%EXPECTED_THUMBPRINT%" ^
    -StoreScope LocalMachine ^
    -InstallTrustedPublisher:$true ^
    -InstallRoot:$true

set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
    echo [ERRO] Falha na instalacao do certificado. Codigo: %RC%
    exit /b %RC%
)

echo Certificado instalado com sucesso.
exit /b 0
