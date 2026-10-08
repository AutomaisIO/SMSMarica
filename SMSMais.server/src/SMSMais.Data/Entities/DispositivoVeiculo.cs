namespace SMSMais.Data.Entities;

/// <summary>
/// Tablet fixo num <see cref="Veiculo"/> que manda a posição do carro para o Mapa da frota
/// (docs/modulos/tfd/deslocamento-tablet.md). Um por veículo. Ativado por código curto gerado no
/// painel; depois fala com o token próprio (só o hash fica no banco).
/// </summary>
public class DispositivoVeiculo
{
    public Guid Id { get; set; }
    public Guid VeiculoId { get; set; }

    /// <summary>SHA-256 do token do tablet. Null enquanto não ativado (ou depois de desvinculado).</summary>
    public string? TokenHash { get; set; }

    /// <summary>SHA-256 do código de ativação pendente (vale até <see cref="CodigoExpiraEm"/>).</summary>
    public string? CodigoHash { get; set; }
    public DateTime? CodigoExpiraEm { get; set; }

    public string? Modelo { get; set; }
    public string? Identificador { get; set; }
    public DateTime? AtivadoEm { get; set; }
    public DateTime? UltimoContatoEm { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public Veiculo? Veiculo { get; set; }
}
