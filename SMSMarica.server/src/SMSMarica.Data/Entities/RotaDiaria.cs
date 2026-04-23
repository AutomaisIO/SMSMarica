using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

public class RotaDiaria
{
    public Guid Id { get; set; }
    public DateOnly Data { get; set; }
    public Guid VeiculoId { get; set; }
    public Guid MotoristaId { get; set; }
    public StatusRota Status { get; set; } = StatusRota.Planejada;
    public DateTime CriadoEm { get; set; }
    public DateTime? IniciadaEm { get; set; }
    public DateTime? ConcluidaEm { get; set; }

    public Veiculo? Veiculo { get; set; }
    public Motorista? Motorista { get; set; }
    public List<Alocacao> Alocacoes { get; set; } = [];
}
