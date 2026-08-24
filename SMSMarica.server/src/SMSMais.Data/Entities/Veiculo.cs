using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

public class Veiculo
{
    public Guid Id { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string Fabricante { get; set; } = string.Empty;
    public string Cor { get; set; } = string.Empty;
    public TipoVeiculo Tipo { get; set; } = TipoVeiculo.Carro;
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public List<Fileira> Fileiras { get; set; } = [];
}
