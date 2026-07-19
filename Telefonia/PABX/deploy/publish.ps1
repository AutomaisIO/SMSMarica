# Publica o Automais.Pabx self-contained para linux-x64 em .\publish.
# Depois: enviar ao servidor (ver docs/runbook-deploy.md) e reiniciar o systemd unit.
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent

dotnet publish "$raiz\src\Automais.Pabx.Api" `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o "$raiz\publish"

Write-Host "OK: $raiz\publish (enviar ao servidor em /opt/automais-pabx)"
