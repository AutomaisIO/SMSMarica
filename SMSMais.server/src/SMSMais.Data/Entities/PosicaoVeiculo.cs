namespace SMSMais.Data.Entities;

/// <summary>
/// Posição de um <see cref="Veiculo"/> enviada pelo tablet fixo nele (<see cref="DispositivoVeiculo"/>).
/// Separada de <see cref="PontoGps"/>, que é do motorista (app logado) e exige motorista.
/// </summary>
public class PosicaoVeiculo
{
    public Guid Id { get; set; }
    public Guid VeiculoId { get; set; }
    public Guid DispositivoId { get; set; }
    public Gps Coordenada { get; set; } = new(0, 0);
    public double? VelocidadeKmh { get; set; }
    public double? Rumo { get; set; }
    public double? PrecisaoM { get; set; }
    public DateTime CapturadoEm { get; set; }
    public DateTime CriadoEm { get; set; }
}
