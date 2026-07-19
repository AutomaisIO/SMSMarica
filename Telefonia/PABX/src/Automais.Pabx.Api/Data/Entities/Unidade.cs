namespace Automais.Pabx.Api.Data.Entities;

/// <summary>
/// Unidade de saúde conectada ao hub WireGuard. Espelho do registro-mestre
/// Telefonia/registro/unidades.csv — o Id é o id da unidade no plano de
/// endereçamento (LAN dos telefones = 10.200.&lt;Id&gt;.0/24).
/// </summary>
public sealed class Unidade
{
    public int Id { get; set; }
    public required string Nome { get; set; }
    public required string Grupo { get; set; }
    public required string Lan { get; set; }
    public required string GatewayMk { get; set; }
    public required string TunnelIp { get; set; }
    public required string Status { get; set; }
    public string? Endereco { get; set; }
    public string? Gestor { get; set; }

    public ICollection<Ramal> Ramais { get; set; } = [];
}
