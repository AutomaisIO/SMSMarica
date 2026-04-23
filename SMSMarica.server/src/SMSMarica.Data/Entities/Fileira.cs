namespace SMSMarica.Data.Entities;

public class Fileira
{
    public Guid Id { get; set; }
    public Guid VeiculoId { get; set; }
    public int Ordem { get; set; }
    public int QuantidadeAssentos { get; set; }
    public DateTime CriadoEm { get; set; }

    public Veiculo? Veiculo { get; set; }
    public List<Assento> Assentos { get; set; } = [];
}
