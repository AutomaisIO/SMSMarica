namespace SMSMais.Data.Entities;

/// <summary>
/// Código de uso único que a central gera no painel para um motorista entrar no tablet de um veículo
/// quando o rosto não resolve (ADR-0073). Vale por pouco tempo e cai depois de tentativas erradas.
/// </summary>
public class LiberacaoMotorista
{
    public Guid Id { get; set; }
    public Guid VeiculoId { get; set; }
    public Guid MotoristaId { get; set; }

    /// <summary>SHA-256 (hex) de "veiculoId:código" — o código em si não fica guardado.</summary>
    public string CodigoHash { get; set; } = string.Empty;

    public DateTime CriadaEm { get; set; }
    public Guid? CriadaPor { get; set; }
    public DateTime ExpiraEm { get; set; }
    public int TentativasErradas { get; set; }

    public DateTime? UsadaEm { get; set; }
    public Guid? UsadaPorDispositivoId { get; set; }
}
