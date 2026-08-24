using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

public class RotaDiaria
{
    public Guid Id { get; set; }
    public DateOnly Data { get; set; }
    public Guid VeiculoId { get; set; }
    public Guid MotoristaId { get; set; }
    public StatusRota Status { get; set; } = StatusRota.Planejada;

    // Geração automática (FT3)
    public OrigemRota Origem { get; set; } = OrigemRota.Manual;
    public DateTime? GeradaEm { get; set; }
    public int? DistanciaTotalMetros { get; set; }
    public int? DuracaoEstimadaSegundos { get; set; }
    public string? PlanoRotaJson { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? IniciadaEm { get; set; }
    public DateTime? ConcluidaEm { get; set; }

    public Veiculo? Veiculo { get; set; }
    public Motorista? Motorista { get; set; }
    public List<Alocacao> Alocacoes { get; set; } = [];
}
