namespace SMSMarica.Secretario.Api.Painel;

/// <summary>Conexão com o Oracle do Salux. Credencial SÓ por env (Salux__Usuario/Salux__Senha) ou appsettings.Local.json (gitignored).</summary>
public sealed class SaluxOpcoes
{
    public string Host { get; set; } = "10.50.0.18";
    public int Porta { get; set; } = 1521;
    public string Servico { get; set; } = "ORASX01";
    public string Usuario { get; set; } = "";
    public string Senha { get; set; } = "";
    public int Hospital { get; set; } = 1; // HMCML
}

public sealed class PainelOpcoes
{
    public int IntervaloRapidoSegundos { get; set; } = 60;
    public int IntervaloLentoSegundos { get; set; } = 600;
    public int TimeoutConsultaSegundos { get; set; } = 120;
    public string SnapshotPath { get; set; } = "snapshot-painel.json";
}
