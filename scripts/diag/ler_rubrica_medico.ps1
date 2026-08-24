# Diagnóstico READ-ONLY: lê a rubrica de assinatura + snapshot de laudo de um médico
# (apenas SELECT; nunca escreve). Salva a imagem real em C:\Automais\rubrica_real.png.
# Uso: & ler_rubrica_medico.ps1 -MedicoId <guid>
param([Parameter(Mandatory = $true)][string]$MedicoId)

$ErrorActionPreference = 'Stop'

Push-Location "$PSScriptRoot\..\..\SMSMais.server\src\SMSMais.Api"
$cs = (dotnet user-secrets list | Select-String "ConnectionStrings:DefaultDb").ToString() -replace '^ConnectionStrings:DefaultDb = ', ''
$dll = Get-ChildItem -Recurse bin -Filter Npgsql.dll | Select-Object -First 1
Pop-Location

Add-Type -Path $dll.FullName
$conn = New-Object Npgsql.NpgsqlConnection($cs)
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT formato, content_type, length(imagem_base64), imagem_base64, atualizado_em, criado_em FROM smsmarica.assinatura_medico WHERE medico_id=@m AND excluido_em IS NULL"
    [void]$cmd.Parameters.AddWithValue("m", [guid]$MedicoId)
    $r = $cmd.ExecuteReader()
    if ($r.Read()) {
        $fmt = $r.GetInt32(0); $ct = $r.GetString(1); $len = $r.GetInt32(2); $img = $r.GetString(3); $atu = $r.GetValue(4)
        "RUBRICA OK | formato=$fmt | type=$ct | b64len=$len | atualizada=$atu"
        $b64 = if ($img -like 'data:*') { $img.Substring($img.IndexOf(',') + 1) } else { $img }
        New-Item -ItemType Directory -Force "C:\Automais" | Out-Null
        [IO.File]::WriteAllBytes("C:\Automais\rubrica_real.png", [Convert]::FromBase64String($b64))
        "Imagem salva em C:\Automais\rubrica_real.png"
    }
    else { "RUBRICA NAO ENCONTRADA" }
    $r.Close()

    $cmd2 = $conn.CreateCommand()
    $cmd2.CommandText = "SELECT medico_nome_snapshot, medico_crm_snapshot, medico_uf_crm_snapshot, medico_rqe_snapshot FROM smsmarica.laudo WHERE medico_id=@m AND medico_nome_snapshot IS NOT NULL ORDER BY criado_em DESC LIMIT 1"
    [void]$cmd2.Parameters.AddWithValue("m", [guid]$MedicoId)
    $r2 = $cmd2.ExecuteReader()
    if ($r2.Read()) { "SNAPSHOT | nome='$($r2.GetValue(0))' crm='$($r2.GetValue(1))' uf='$($r2.GetValue(2))' rqe='$($r2.GetValue(3))'" }
    else { "SEM LAUDO COM SNAPSHOT" }
    $r2.Close()
}
finally { $conn.Close() }
