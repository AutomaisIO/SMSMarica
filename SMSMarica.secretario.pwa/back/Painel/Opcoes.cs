namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Como o painel alcança as bases: pelo proxy SQL interno do smsmarica (porta de loopback
/// + token). O painel NÃO guarda credencial de banco nem driver — manda o slug da base e o
/// SQL, e o smsmarica resolve o resto (Oracle direto no HMCML, SQL Server via agente na UPA).
/// Token SÓ por env (ProxySql__Token) ou appsettings.Local.json (gitignored).
/// </summary>
public sealed class ProxySqlOpcoes
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:5091";
    public string Token { get; set; } = "";
    public int MaxLinhas { get; set; } = 500;
}

public sealed class PainelOpcoes
{
    public int IntervaloRapidoSegundos { get; set; } = 60;
    public int IntervaloLentoSegundos { get; set; } = 600;
    public int TimeoutConsultaSegundos { get; set; } = 120;
    public string SnapshotPath { get; set; } = "snapshot-painel.json";
}
