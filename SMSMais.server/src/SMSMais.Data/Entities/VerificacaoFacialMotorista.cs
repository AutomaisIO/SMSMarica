using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Cada conferência do motorista no tablet (ADR-0073), aprovada ou não: login, início de translado,
/// primeiro acesso ou liberação pela central. "Quem está dirigindo" é a última aprovada do veículo.
/// Chega pela fila sem rede do tablet; <see cref="IdLocal"/> impede duplicar.
/// </summary>
public class VerificacaoFacialMotorista
{
    public Guid Id { get; set; }
    public Guid IdLocal { get; set; }
    public Guid VeiculoId { get; set; }
    public Guid DispositivoId { get; set; }

    /// <summary>Motorista conferido (ou o candidato mais parecido, quando reprovada). Nulo quando
    /// não havia com quem comparar.</summary>
    public Guid? MotoristaId { get; set; }
    public Guid? RotaId { get; set; }

    public MomentoVerificacaoFacial Momento { get; set; }
    public MeioVerificacaoMotorista Meio { get; set; }
    public bool Aprovada { get; set; }

    /// <summary>Similaridade de cosseno e o limiar usado no tablet (só no meio Rosto).</summary>
    public double? Nota { get; set; }
    public double? Limiar { get; set; }
    public bool ProvaDeVida { get; set; }
    public string? ModeloVersao { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? PrecisaoM { get; set; }

    /// <summary>Hora no tablet (UTC) e de chegada ao servidor.</summary>
    public DateTime CapturadaEm { get; set; }
    public DateTime RecebidaEm { get; set; }

    /// <summary>Selfie reduzida (JPEG base64). Apagada pela rotina de retenção.</summary>
    public string? SelfieBase64 { get; set; }
    public DateTime? SelfieApagadaEm { get; set; }

    /// <summary>Código da central usado, quando o meio é <see cref="MeioVerificacaoMotorista.CodigoCentral"/>.</summary>
    public Guid? LiberacaoId { get; set; }
}
