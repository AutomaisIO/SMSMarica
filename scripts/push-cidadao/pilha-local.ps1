<#
.SYNOPSIS
    Sobe a pilha LOCAL de teste do push (hub FHIR + API + painel) apontada para a BANCADA (banco
    de testes compartilhado) — nunca para a produção.

    Precisa de duas variáveis de ambiente (processo ou usuário), que NÃO ficam no repositório:
      SMSMARICA_TESTS_CONNECTION  connection da bancada (a mesma que os testes de banco usam)
      SMSMAIS_BANCADA_HOST        host EXATO da bancada; a trava só deixa passar esse host

.DESCRIPTION
    Cada serviço abre na sua própria janela, na ordem hub -> API -> painel, e o script espera
    cada porta responder antes de seguir. As variáveis de ambiente (connection, chaves de teste)
    são definidas DENTRO da janela de cada serviço: a janela chama este mesmo script com
    -Servico, relê a connection e refaz a trava de host. Nada de senha na linha de comando de
    processo nenhum, nada de variável vazando para o console de quem chamou.

    Por que a trava existe: o user-secrets "smsmarica-api-dev" da API aponta para a PRODUÇÃO e o
    appsettings.json tem AutoMigrate:Enabled=true. Subir a API sem sobrescrever o banco aplicaria
    migration e seed em produção. Por isso a API sobe FORA de Development (ASPNETCORE_ENVIRONMENT
    =Bancada): fora de Development o ASP.NET nem carrega user-secrets — se a variável da
    connection falhar por qualquer motivo, sobra o "DefaultDb": "" do appsettings.json (string
    vazia, sem host): a migration falha no log, o /health não dá 200 e a espera aborta, em vez de
    cair na produção.

    Os PIDs das três janelas ficam em %LOCALAPPDATA%\SMSMais\pilha-local\pilha-local.pids.txt
    (fora do repositório); -Parar derruba as janelas e tudo que rodou dentro delas (dotnet, node)
    — menos os servidores de build compartilhados (nó do MSBuild, VBCSCompiler), que atendem os
    builds de outras sessões.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\push-cidadao\pilha-local.ps1
    powershell -ExecutionPolicy Bypass -File scripts\push-cidadao\pilha-local.ps1 -Aparelho <serial do 'adb devices'>
    powershell -ExecutionPolicy Bypass -File scripts\push-cidadao\pilha-local.ps1 -Parar
#>
[CmdletBinding()]
param(
    # Derruba as janelas que a última subida abriu.
    [switch]$Parar,

    # Uso interno: a janela de cada serviço chama o script de novo com o serviço que vai rodar.
    [ValidateSet('', 'hub', 'api', 'front')]
    [string]$Servico = '',

    # Celular de teste (adb reverse): o serial de 'adb devices'. Vazio = o único celular conectado.
    [string]$Aparelho = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
# A barra de progresso do Invoke-WebRequest no 5.1 deixa cada sondagem de porta lenta.
$ProgressPreference = 'SilentlyContinue'

# ---------------------------------------------------------------------------------------------
# Onde as coisas estão
# ---------------------------------------------------------------------------------------------

# Raiz do clone onde este script está (scripts/push-cidadao/ → dois níveis acima), com a branch
# do push. O painel sobe DESTE clone: sem .env.local ele usa o proxy do Vite para a API local.
# Se algum .env* do SMSMais.front apontar para outro backend, o script aborta (Conferir-EnvFront).
$RaizWorktree = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).ProviderPath
$PastaHub     = Join-Path $RaizWorktree 'Automais.Fhir'
$PastaServer  = Join-Path $RaizWorktree 'SMSMais.server'
$PastaFront   = Join-Path $RaizWorktree 'SMSMais.front'
$PastaApp     = Join-Path $RaizWorktree 'SMSMais.cidadao.app'

# PIDs e marcadores ficam fora do repositório, para nunca entrarem num commit. Pasta conhecida do
# Windows (não a variável TEMP, que cada shell pode redefinir): o -Parar de qualquer terminal acha
# o que a subida registrou.
$PastaEstado = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SMSMais\pilha-local'
New-Item -ItemType Directory -Path $PastaEstado -Force | Out-Null
$ArquivoPids = Join-Path $PastaEstado 'pilha-local.pids.txt'

function Ler-Variavel([string]$Nome) {
    foreach ($escopo in 'Process', 'User') {
        $v = [Environment]::GetEnvironmentVariable($Nome, $escopo)
        if (-not [string]::IsNullOrWhiteSpace($v)) { return @{ Valor = $v.Trim(); Escopo = $escopo } }
    }
    return $null
}

# ---------------------------------------------------------------------------------------------
# A trava: só a bancada passa
# ---------------------------------------------------------------------------------------------

# Bancada = Postgres de testes num cluster SEPARADO do da produção, mas no mesmo provedor: o nome do
# provedor no host não distingue nada. O que vale é o host EXATO, que vem de SMSMAIS_BANCADA_HOST
# (fora do repositório, que é público). Os trechos proibidos são uma segunda camada — barram o host
# da produção mesmo que alguém o ponha nessa variável — e fazem a mensagem dizer que aquilo é produção.
$HostBancada = ''
$hb = Ler-Variavel 'SMSMAIS_BANCADA_HOST'
if ($null -ne $hb) { $HostBancada = $hb.Valor }
$PortaBancada     = '25060'
$TrechosProibidos = @('smsmarica', 'smsmais', 'psql01', 'eveo')

# O hub precisa de um banco próprio: no defaultdb da bancada o hub está atrasado (o unaccent
# mora em public e a migration AddPatientTrgmSearch não acha smsmarica.unaccent) e o boot aborta.
$BancoHub = 'fhir_testes'

$PortaHub   = 5081   # a API procura o hub aqui (Fhir:BaseUrl); o launchSettings do hub usa 5134
$PortaApi   = 5080
$PortaFront = 5173

# Fora de Development de propósito: o user-secrets da API (que aponta para a produção) só é
# carregado em Development. Nenhum código do server ou do hub depende do nome do ambiente além
# do DetailedErrors, que vai ligado por variável.
$AmbienteAspNet = 'Bancada'

# Teto de conexões de cada serviço: a bancada é compartilhada com as suítes de teste.
$PoolApi = 10
$PoolHub = 5

# Esperas (segundos). A primeira subida compila; a API ainda aplica as migrations antes de abrir a porta.
$EsperaHub   = 300
$EsperaApi   = 600
$EsperaFront = 120

# ---------------------------------------------------------------------------------------------
# Utilidades
# ---------------------------------------------------------------------------------------------

function Caminho-Marcador([string]$Qual) {
    # A janela grava este arquivo quando o serviço termina (com o código de saída): a espera
    # do script principal para na hora, em vez de gastar o timeout inteiro.
    return (Join-Path $PastaEstado ("pilha-local.{0}.saiu" -f $Qual))
}

function Abortar([string]$Mensagem) {
    Write-Host ''
    Write-Host ("ABORTADO: {0}" -f $Mensagem) -ForegroundColor Red
    if ($Servico) {
        try { Set-Content -LiteralPath (Caminho-Marcador $Servico) -Value 'abortado' -Encoding ASCII } catch { }
    }
    exit 1
}

function Aviso([string]$Mensagem) {
    Write-Host ("AVISO: {0}" -f $Mensagem) -ForegroundColor Yellow
}

function Ler-ConexaoBruta {
    $v = Ler-Variavel 'SMSMARICA_TESTS_CONNECTION'
    if ($null -eq $v) { return $null }
    return @{ Texto = $v.Valor; Origem = ("variável SMSMARICA_TESTS_CONNECTION ({0})" -f $v.Escopo) }
}

function Novo-Builder([string]$Texto) {
    # Métodos explícitos (set_/get_): o PowerShell trata o builder como dicionário e o
    # indexador $b['Host'] não funciona nele.
    $b = New-Object System.Data.Common.DbConnectionStringBuilder
    $b.set_ConnectionString($Texto)
    return , $b
}

function Valor-Chave($Builder, [string[]]$Chaves) {
    foreach ($c in $Chaves) {
        $v = $null
        if ($Builder.TryGetValue($c, [ref]$v) -and -not [string]::IsNullOrWhiteSpace([string]$v)) {
            return ([string]$v).Trim()
        }
    }
    return ''
}

function Sem-Senha([string]$Texto) {
    $b = Novo-Builder $Texto
    foreach ($c in 'password', 'pwd', 'psw') { [void]$b.Remove($c) }
    return $b.get_ConnectionString()
}

function Obter-ConexaoBancada {
    if (-not $HostBancada) {
        Abortar ("defina SMSMAIS_BANCADA_HOST com o host EXATO da bancada (processo ou usuário; o script lê o " +
                 "escopo de usuário direto, não precisa abrir outro terminal): " +
                 "[Environment]::SetEnvironmentVariable('SMSMAIS_BANCADA_HOST', '<host>', 'User')")
    }
    $bruta = Ler-ConexaoBruta
    if ($null -eq $bruta) {
        Abortar (("a variável SMSMARICA_TESTS_CONNECTION está vazia (processo e usuário). Defina com a connection " +
                  "da BANCADA (host {0}, porta {1}): [Environment]::SetEnvironmentVariable('SMSMARICA_TESTS_CONNECTION', " +
                  "'Host={0};Port={1};Database=<banco>;Username=<usuario>;Password=<senha>;SSL Mode=Require', 'User')") -f $HostBancada, $PortaBancada)
    }

    try { $b = Novo-Builder $bruta.Texto } catch { Abortar ("connection ilegível em {0}." -f $bruta.Origem) }

    # Npgsql aceita Host e Server; as duas contam. Mais de uma, ou lista de hosts, não passa.
    $hosts = @()
    foreach ($c in 'host', 'server', 'data source') {
        $v = Valor-Chave $b @($c)
        if ($v) { $hosts += $v }
    }
    if ($hosts.Count -ne 1) {
        Abortar ("a connection de {0} tem {1} host(s) declarados; esperava exatamente um." -f $bruta.Origem, $hosts.Count)
    }
    $hostBanco = $hosts[0]
    $minusculo = $hostBanco.ToLowerInvariant()
    foreach ($t in $TrechosProibidos) {
        if ($minusculo.Contains($t)) {
            Abortar ("o host '{0}' contém '{1}': isso é PRODUÇÃO. Nada subiu." -f $hostBanco, $t)
        }
    }
    if ($minusculo -ne $HostBancada.ToLowerInvariant()) {
        Abortar ("o host '{0}' não é o da bancada ({1}). Nada subiu." -f $hostBanco, $HostBancada)
    }
    $porta = Valor-Chave $b @('port')
    if ($porta -ne $PortaBancada) {
        Abortar ("porta '{0}' não é a da bancada ({1}). Nada subiu." -f $porta, $PortaBancada)
    }

    $hub = Novo-Builder $b.get_ConnectionString()
    $hub.set_Item('database', $BancoHub)

    return @{
        Origem     = $bruta.Origem
        Host       = $hostBanco
        Porta      = $porta
        BancoApi   = (Valor-Chave $b @('database', 'db'))
        Usuario    = (Valor-Chave $b @('username', 'user id', 'userid', 'user'))
        Conexao    = $b.get_ConnectionString()
        ConexaoHub = $hub.get_ConnectionString()
    }
}

function Conferir-EnvFront {
    # Em modo dev o Vite lê estes arquivos. Qualquer VITE_API_BASE_URL que não seja local faria o
    # painel pular o proxy e falar direto com outro backend.
    foreach ($nome in '.env', '.env.local', '.env.development', '.env.development.local') {
        $arq = Join-Path $PastaFront $nome
        if (-not (Test-Path -LiteralPath $arq)) { continue }
        foreach ($linha in (Get-Content -LiteralPath $arq)) {
            if ($linha -match '^\s*VITE_API_BASE_URL\s*=\s*(.*)$') {
                $valor = $Matches[1].Trim().Trim('"').Trim("'")
                if ($valor -and $valor -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?/?$') {
                    Abortar ("{0} define VITE_API_BASE_URL={1}. O painel tem de usar o proxy local (/api -> :{2})." -f $arq, $valor, $PortaApi)
                }
            }
        }
    }
}

function Porta-Ocupada([int]$Porta) {
    try {
        $c = @(Get-NetTCPConnection -State Listen -LocalPort $Porta -ErrorAction SilentlyContinue)
    } catch {
        return ''
    }
    if ($c.Count -eq 0) { return '' }
    $nome = '?'
    try { $nome = (Get-Process -Id $c[0].OwningProcess).ProcessName } catch { }
    return ("{0} (PID {1})" -f $nome, $c[0].OwningProcess)
}

# ---------------------------------------------------------------------------------------------
# PIDs das janelas
# ---------------------------------------------------------------------------------------------

function Ler-Pids {
    # Uma linha por janela: servico|pid|inicio (ticks UTC). O início protege contra PID reaproveitado.
    $lista = @()
    if (-not (Test-Path -LiteralPath $ArquivoPids)) { return , $lista }
    foreach ($linha in (Get-Content -LiteralPath $ArquivoPids)) {
        $p = $linha.Split('|')
        if ($p.Count -eq 3) { $lista += @{ Servico = $p[0]; Pid = [int]$p[1]; Inicio = $p[2] } }
    }
    return , $lista
}

function Processo-Nosso($Item) {
    $proc = Get-Process -Id $Item.Pid -ErrorAction SilentlyContinue
    if ($null -eq $proc) { return $null }
    $inicio = ''
    try { $inicio = [string]$proc.StartTime.ToUniversalTime().Ticks } catch { }
    if ($inicio -ne $Item.Inicio) { return $null }
    return $proc
}

function Registrar-Pid([string]$Qual, $Proc) {
    $inicio = [string]$Proc.StartTime.ToUniversalTime().Ticks
    Add-Content -LiteralPath $ArquivoPids -Value ("{0}|{1}|{2}" -f $Qual, $Proc.Id, $inicio) -Encoding ASCII
}

function Servidor-DeBuild($Proc) {
    # Nó do MSBuild com reuso e VBCSCompiler nascem do nosso dotnet run, mas depois atendem QUALQUER
    # build do usuário (outras sessões inclusive): derrubar um deles quebra o build alheio no meio
    # (MSB4166). Eles saem sozinhos após alguns minutos ociosos.
    $linha = [string]$Proc.CommandLine
    if ($linha -match '(?i)VBCSCompiler\.(dll|exe)') { return $true }
    return ($linha -match '(?i)MSBuild\.(dll|exe)' -and $linha -match '(?i)[/-]nodemode:')
}

function Descendentes($Todos, [int]$RaizPid, [datetime]$RaizCriacao) {
    # Árvore pelo ParentProcessId, aceitando só filho criado DEPOIS do pai: o Windows reaproveita
    # PID, e um órfão antigo pode ter como "pai" um número que hoje é da nossa janela (o
    # taskkill /T não confere isso). Devolve as folhas primeiro.
    $achados = New-Object System.Collections.ArrayList
    $vistos  = @{ $RaizPid = $true }
    $fila    = New-Object System.Collections.Queue
    $fila.Enqueue(@($RaizPid, $RaizCriacao))
    while ($fila.Count -gt 0) {
        $pai = $fila.Dequeue()
        foreach ($p in $Todos) {
            $id = [int]$p.ProcessId
            if ($vistos.ContainsKey($id) -or [int]$p.ParentProcessId -ne $pai[0]) { continue }
            if ($null -eq $p.CreationDate -or $p.CreationDate -lt $pai[1]) { continue }
            if (Servidor-DeBuild $p) { continue }
            $vistos[$id] = $true
            [void]$achados.Add($p)
            $fila.Enqueue(@($id, $p.CreationDate))
        }
    }
    $achados.Reverse()
    return $achados   # sai item a item; quem chama junta com @()
}

function Encerrar-SeForNosso($Proc) {
    # Confere de novo pelo horário de criação: entre a foto e o kill o PID pode ter trocado de dono.
    $agora = Get-CimInstance -ClassName Win32_Process -Filter ("ProcessId={0}" -f $Proc.ProcessId) -ErrorAction SilentlyContinue
    if ($null -eq $agora -or $agora.CreationDate -ne $Proc.CreationDate) { return $true }
    try {
        Stop-Process -Id $Proc.ProcessId -Force -ErrorAction Stop
        return $true
    } catch {
        return ($null -eq (Get-Process -Id $Proc.ProcessId -ErrorAction SilentlyContinue))
    }
}

function Parar-Pilha {
    $lista = Ler-Pids
    if ($lista.Count -eq 0) {
        Write-Host ("Nenhuma janela registrada em {0}: nada a parar." -f $ArquivoPids)
        return
    }
    # Uma foto só de todos os processos: pai, horário de criação e linha de comando.
    $todos = @(Get-CimInstance -ClassName Win32_Process -Property ProcessId, ParentProcessId, CreationDate, CommandLine)
    foreach ($item in $lista) {
        $janela = Processo-Nosso $item
        if ($null -eq $janela) {
            Write-Host ("  {0}: PID {1} já tinha saído (ou o número agora é de outro processo; não mexi)." -f $item.Servico, $item.Pid)
            continue
        }
        $raiz = @($todos | Where-Object { [int]$_.ProcessId -eq $item.Pid })
        $dentro = @()
        if ($raiz.Count -eq 1) { $dentro = @(Descendentes $todos $item.Pid $raiz[0].CreationDate) }
        # Primeiro o que rodou dentro da janela (dotnet run -> app; npm -> node/vite), folhas antes.
        $resistiram = @()
        foreach ($p in $dentro) {
            if (-not (Encerrar-SeForNosso $p)) { $resistiram += $p.ProcessId }
        }
        # A janela por último, conferida de novo pelo início registrado.
        $janela = Processo-Nosso $item
        if ($null -ne $janela) {
            try { Stop-Process -Id $item.Pid -Force -ErrorAction Stop; [void]$janela.WaitForExit(5000) } catch { }
        }
        if ($resistiram.Count -gt 0) {
            Aviso ("{0}: não consegui encerrar o(s) PID(s) {1}." -f $item.Servico, ($resistiram -join ', '))
        } elseif ($null -eq (Processo-Nosso $item)) {
            Write-Host ("  {0}: janela PID {1} encerrada (+{2} processo(s) dentro dela)." -f $item.Servico, $item.Pid, $dentro.Count) -ForegroundColor Green
        }
    }
    # Só esquece o que de fato saiu: janela que resistiu continua no arquivo para o próximo -Parar.
    $vivos = @($lista | Where-Object { $null -ne (Processo-Nosso $_) })
    if ($vivos.Count -eq 0) {
        Remove-Item -LiteralPath $ArquivoPids -Force
    } else {
        Set-Content -LiteralPath $ArquivoPids -Encoding ASCII -Value @($vivos | ForEach-Object { "{0}|{1}|{2}" -f $_.Servico, $_.Pid, $_.Inicio })
        Aviso ("{0} janela(s) continuam no ar; rode -Parar de novo." -f $vivos.Count)
    }
    foreach ($q in 'hub', 'api', 'front') { Remove-Item -LiteralPath (Caminho-Marcador $q) -Force -ErrorAction SilentlyContinue }
}

# ---------------------------------------------------------------------------------------------
# Dentro de cada janela
# ---------------------------------------------------------------------------------------------

function Rodar-Servico([string]$Qual) {
    $marcador = Caminho-Marcador $Qual
    Remove-Item -LiteralPath $marcador -Force -ErrorAction SilentlyContinue
    # Daqui em diante só roda programa nativo; com 'Stop' o 5.1 transforma stderr em erro.
    $ErrorActionPreference = 'Continue'

    switch ($Qual) {
        'hub' {
            $c = Obter-ConexaoBancada
            $Host.UI.RawUI.WindowTitle = ("Pilha local - hub FHIR :{0} (bancada / {1})" -f $PortaHub, $BancoHub)
            $env:ASPNETCORE_ENVIRONMENT    = $AmbienteAspNet
            $env:DOTNET_ENVIRONMENT        = $AmbienteAspNet
            $env:ConnectionStrings__FhirDb = $c.ConexaoHub
            $env:Db__MaxPoolSize           = [string]$PoolHub
            Write-Host ("hub FHIR -> {0}:{1} banco {2}" -f $c.Host, $c.Porta, $BancoHub) -ForegroundColor Cyan
            Set-Location -LiteralPath $PastaHub
            & dotnet run --project 'src\Automais.Fhir.Api' --no-launch-profile -- --urls ("http://localhost:{0}" -f $PortaHub)
        }
        'api' {
            $c = Obter-ConexaoBancada
            $Host.UI.RawUI.WindowTitle = ("Pilha local - API :{0} (bancada / {1})" -f $PortaApi, $c.BancoApi)
            $env:ASPNETCORE_ENVIRONMENT      = $AmbienteAspNet
            $env:DOTNET_ENVIRONMENT          = $AmbienteAspNet
            $env:ConnectionStrings__DefaultDb = $c.Conexao
            $env:Db__MaxPoolSize             = [string]$PoolApi
            # Bancada é descartável: aplica a migration do push ali (aditiva e nullable).
            $env:AutoMigrate__Enabled        = 'true'
            # Program.cs: tira os hosted services SMSMais.* (varreduras, robô, envios, sincronizações).
            $env:Workers__Desligados         = 'true'
            # PacienteAuthService/TelefoneValidacaoService: o código do login volta na tela.
            $env:Tfd__Otp__ModoTeste         = 'true'
            # WhatsAppCliente/ZapMidiaCliente: nada sai para a Meta, só grava "[SIMULADO]".
            $env:Tfd__WhatsApp__Simular      = 'true'
            $env:Fhir__BaseUrl               = ("http://localhost:{0}/" -f $PortaHub)
            # O que o appsettings.Development.json daria, já que aqui o ambiente não é Development.
            $env:DetailedErrors              = 'true'
            $env:Anexos__PwaBaseUrl          = 'http://localhost:5175'
            # O appsettings aponta para o PACS de verdade; aqui ele fica fora de alcance de propósito.
            $env:Pacs__Dcm4chee__RsBaseUrl       = 'http://127.0.0.1:9/dcm4chee-arc/aets/PACS-CDT/rs/'
            $env:Pacs__Dcm4chee__WorklistBaseUrl = 'http://127.0.0.1:9/dcm4chee-arc/aets/WORKLIST/rs/'
            Write-Host ("API -> {0}:{1} banco {2}; hub em {3}" -f $c.Host, $c.Porta, $c.BancoApi, $env:Fhir__BaseUrl) -ForegroundColor Cyan
            Write-Host 'Confirme no log: "Workers:Desligados — N rotinas de fundo NÃO vão rodar".' -ForegroundColor Cyan
            # Program.cs engole a falha da migration (só loga) e o /health só testa a conexão: a
            # espera dá "ok" mesmo com a migration do push faltando.
            Write-Host 'Confirme no log: "Migrations OK." (se vier "Falha aplicando migrations", o /health ainda dá 200).' -ForegroundColor Cyan
            Set-Location -LiteralPath $PastaServer
            & dotnet run --project 'src\SMSMais.Api' --no-launch-profile -- --urls ("http://localhost:{0}" -f $PortaApi)
        }
        'front' {
            Conferir-EnvFront
            $Host.UI.RawUI.WindowTitle = ("Pilha local - painel :{0} (proxy /api -> :{1})" -f $PortaFront, $PortaApi)
            # Sem VITE_API_BASE_URL o httpClient usa /api, que o vite.config.ts manda para localhost:5080.
            Remove-Item Env:VITE_API_BASE_URL -ErrorAction SilentlyContinue
            Set-Location -LiteralPath $PastaFront
            & npm.cmd run dev -- --port $PortaFront --strictPort
        }
    }

    $codigo = $LASTEXITCODE
    Set-Content -LiteralPath $marcador -Value ([string]$codigo) -Encoding ASCII
    Write-Host ''
    Write-Host ("{0} terminou (código {1}). A janela fica aberta para ler o log." -f $Qual, $codigo) -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------------------------
# Script principal
# ---------------------------------------------------------------------------------------------

function Abrir-Janela([string]$Qual) {
    $argumentos = @('-NoExit', '-NoProfile', '-ExecutionPolicy', 'Bypass',
                    '-File', ('"{0}"' -f $PSCommandPath), '-Servico', $Qual)
    $proc = Start-Process -FilePath 'powershell.exe' -ArgumentList $argumentos -PassThru
    Registrar-Pid $Qual $proc
    Write-Host ("  janela '{0}' aberta (PID {1})." -f $Qual, $proc.Id)
}

function Esperar-Http([string]$Qual, [string]$Url, [int]$TimeoutSeg) {
    $limite = (Get-Date).AddSeconds($TimeoutSeg)
    $ultimo = 'sem resposta'
    $graca = $null   # respondeu, mas não 200: só mais um pouco, e desiste
    Write-Host -NoNewline ("  esperando {0} " -f $Url)
    while ((Get-Date) -lt $limite) {
        $marcador = Caminho-Marcador $Qual
        if (Test-Path -LiteralPath $marcador) {
            # @() -join: no 5.1, -Raw de arquivo vazio (pego no meio da escrita) não devolve nada,
            # e o .Trim() direto (ou depois de [string]) estoura com "método em valor nulo".
            $codigo = (@(Get-Content -LiteralPath $marcador -Raw) -join '').Trim()
            Write-Host ''
            Abortar ("'{0}' terminou antes de responder (código {1}). Leia a janela dele; depois rode -Parar." -f $Qual, $codigo)
        }
        try {
            $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($r.StatusCode -eq 200) { Write-Host ' ok' -ForegroundColor Green; return }
            $ultimo = "HTTP $($r.StatusCode)"
        } catch {
            $ex = $_.Exception
            if ($ex -is [System.Net.WebException] -and $null -ne $ex.Response) {
                $ultimo = "HTTP $([int]$ex.Response.StatusCode)"
            } else {
                $ultimo = $ex.Message
            }
        }
        if ($ultimo -like 'HTTP *') {
            if ($null -eq $graca) { $graca = (Get-Date).AddSeconds(20) }
            elseif ((Get-Date) -gt $graca) { break }
        }
        Write-Host -NoNewline '.'
        Start-Sleep -Seconds 3
    }
    Write-Host ''
    Abortar ("{0} não respondeu 200 (último: {1}). Leia a janela '{2}'; depois rode -Parar." -f $Url, $ultimo, $Qual)
}

function Ligar-Adb {
    $ErrorActionPreference = 'Continue'
    $adb = $null
    $cmd = Get-Command adb.exe -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { $adb = $cmd.Source }
    else {
        $padrao = Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'
        if (Test-Path -LiteralPath $padrao) { $adb = $padrao }
    }
    if ($null -eq $adb) {
        Aviso ("adb não encontrado. Quando tiver: adb -s <serial> reverse tcp:{0} tcp:{0}" -f $PortaApi)
        return
    }
    $linhas = @(& $adb devices | Select-Object -Skip 1 | Where-Object { [string]$_ -match '^\S+\s+\S+' })
    if (-not $Aparelho) {
        # Sem -Aparelho: serve o único celular pronto; com zero ou vários, pede o serial.
        $prontos = @($linhas | Where-Object { [string]$_ -match '^\S+\s+device$' })
        if ($prontos.Count -ne 1) {
            Aviso ("{0} celular(es) pronto(s) no adb. Rode de novo com -Aparelho <serial> (veja 'adb devices'), ou à mão: adb -s <serial> reverse tcp:{1} tcp:{1}" -f $prontos.Count, $PortaApi)
            return
        }
        $script:Aparelho = ([string]$prontos[0] -split '\s+')[0]
    }
    $reverse = ("adb -s {0} reverse tcp:{1} tcp:{1}" -f $Aparelho, $PortaApi)
    $estado = $null
    foreach ($linha in $linhas) {
        if ([string]$linha -match ('^{0}\s+(\S+)' -f [regex]::Escape($Aparelho))) { $estado = $Matches[1] }
    }
    if ($null -eq $estado) {
        Aviso ("celular {0} não está conectado. Se o seu é outro, rode com -Aparelho <serial> (veja 'adb devices'). Ao plugar: {1}" -f $Aparelho, $reverse)
        return
    }
    if ($estado -ne 'device') {
        Aviso ("celular {0} está '{1}' (autorize a depuração USB). Depois: {2}" -f $Aparelho, $estado, $reverse)
        return
    }
    & $adb -s $Aparelho reverse ("tcp:{0}" -f $PortaApi) ("tcp:{0}" -f $PortaApi) | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Host ("  adb reverse tcp:{0} ligado no {1}." -f $PortaApi, $Aparelho) -ForegroundColor Green
    } else {
        Aviso ("adb reverse devolveu {0}. Tente: {1}" -f $LASTEXITCODE, $reverse)
    }
}

function Subir-Pilha {
    # 0) Já tem pilha no ar?
    $anteriores = Ler-Pids
    $vivos = @($anteriores | Where-Object { $null -ne (Processo-Nosso $_) })
    if ($vivos.Count -gt 0) {
        Abortar ("a pilha anterior ainda está no ar ({0} janela(s)). Rode com -Parar antes." -f $vivos.Count)
    }
    Remove-Item -LiteralPath $ArquivoPids -Force -ErrorAction SilentlyContinue
    foreach ($q in 'hub', 'api', 'front') { Remove-Item -LiteralPath (Caminho-Marcador $q) -Force -ErrorAction SilentlyContinue }

    # 1) Connection: só a bancada passa.
    $c = Obter-ConexaoBancada
    Write-Host ''
    Write-Host 'Banco da pilha local (BANCADA, não produção):' -ForegroundColor Cyan
    Write-Host ("  origem : {0}" -f $c.Origem)
    Write-Host ("  host   : {0}:{1}" -f $c.Host, $c.Porta)
    Write-Host ("  usuário: {0}" -f $c.Usuario)
    Write-Host ("  API    : banco {0}" -f $c.BancoApi)
    Write-Host ("  hub    : banco {0}" -f $BancoHub)
    Write-Host ("  (sem senha) {0}" -f (Sem-Senha $c.Conexao))
    Write-Host ''

    # 2) Pastas, front e portas.
    foreach ($p in $PastaHub, $PastaServer, $PastaFront) {
        if (-not (Test-Path -LiteralPath $p)) { Abortar ("pasta não encontrada: {0}" -f $p) }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $PastaFront 'node_modules'))) {
        Abortar ("{0}\node_modules não existe. Rode 'npm install' lá antes." -f $PastaFront)
    }
    Conferir-EnvFront
    if ([Environment]::GetEnvironmentVariable('VITE_API_BASE_URL', 'User')) {
        Aviso 'VITE_API_BASE_URL está definida no seu usuário; a janela do painel apaga a cópia dela antes do npm run dev.'
    }
    foreach ($porta in $PortaHub, $PortaApi, $PortaFront) {
        $dono = Porta-Ocupada $porta
        # Porta ocupada = algo já responde ali (talvez o painel da árvore principal, que fala com a
        # produção). A espera daria "ok" falso; melhor parar e deixar a pessoa ver o que é.
        if ($dono) { Abortar ("a porta {0} já está em uso por {1}. Feche esse processo antes." -f $porta, $dono) }
    }

    # 3) hub -> API -> painel, cada um na sua janela, esperando o anterior responder.
    Write-Host ("[1/3] hub FHIR (:{0})" -f $PortaHub) -ForegroundColor Cyan
    Abrir-Janela 'hub'
    Esperar-Http 'hub' ("http://localhost:{0}/health" -f $PortaHub) $EsperaHub

    Write-Host ("[2/3] API (:{0})" -f $PortaApi) -ForegroundColor Cyan
    Abrir-Janela 'api'
    Esperar-Http 'api' ("http://localhost:{0}/health" -f $PortaApi) $EsperaApi

    Write-Host ("[3/3] painel (:{0})" -f $PortaFront) -ForegroundColor Cyan
    Abrir-Janela 'front'
    Esperar-Http 'front' ("http://localhost:{0}/" -f $PortaFront) $EsperaFront
    # Prova de que o proxy do Vite chega na API local (e não em outro backend).
    Esperar-Http 'front' ("http://localhost:{0}/api/health" -f $PortaFront) 30

    # 4) Celular.
    Write-Host ''
    Write-Host 'Celular' -ForegroundColor Cyan
    Ligar-Adb

    # 5) O que falta: o app.
    Write-Host ''
    Write-Host 'Pilha no ar:' -ForegroundColor Green
    Write-Host ("  hub FHIR  http://localhost:{0}/health" -f $PortaHub)
    Write-Host ("  API       http://localhost:{0}/docs" -f $PortaApi)
    Write-Host ("  painel    http://localhost:{0}" -f $PortaFront)
    Write-Host '  login do app: o código volta na tela (Tfd:Otp:ModoTeste); WhatsApp simulado; rotinas de fundo desligadas.'
    Write-Host '  confira "Migrations OK." na janela da API: o /health dá 200 mesmo se a migration falhar.' -ForegroundColor Yellow
    Write-Host ''
    Write-Host 'App (em outro terminal):' -ForegroundColor Cyan
    Write-Host ("  cd `"{0}`"" -f $PastaApp)
    $serial = $Aparelho
    if (-not $serial) { $serial = '<serial>' }
    Write-Host ("  flutter run -d {0} --dart-define-from-file=firebase.json --dart-define=API_BASE_URL=http://localhost:{1}" -f $serial, $PortaApi)
    Write-Host '  (sem API_BASE_URL o app cai no default https://api.smsmarica.online, que é a PRODUÇÃO)' -ForegroundColor Yellow
    if (-not (Test-Path -LiteralPath (Join-Path $PastaApp 'firebase.json'))) {
        Aviso ("{0}\firebase.json não existe (é ignorado pelo git). Sem ele o --dart-define-from-file falha." -f $PastaApp)
    }
    Write-Host ''
    Write-Host 'Para derrubar as três janelas:' -ForegroundColor Cyan
    Write-Host ("  powershell -ExecutionPolicy Bypass -File `"{0}`" -Parar" -f $PSCommandPath)
}

if ($Parar) {
    Parar-Pilha
} elseif ($Servico) {
    Rodar-Servico $Servico
} else {
    Subir-Pilha
}
