using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// O paciente compartilhando a localização do celular com o motorista enquanto espera um embarque
/// (ADR-0082). Uma linha por sessão e trecho, com só a ÚLTIMA posição, sem trajeto. Ao encerrar
/// (embarque, ausência, "Parar", fim da espera), os campos de posição são apagados e ficam só o
/// começo e o fim. O aceite do paciente vai para a trilha de auditoria.
/// </summary>
public class CompartilhamentoLocalizacao
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }
    public TrechoViagem Trecho { get; set; }

    /// <summary>Sessão do app que aceitou (o <c>jti</c> do token do cidadão).</summary>
    public Guid CidadaoSessaoId { get; set; }

    /// <summary><c>pwa</c> ou <c>nativo</c> (cabeçalho <c>X-App-Canal</c>).</summary>
    public string Canal { get; set; } = string.Empty;

    public DateTime IniciadoEm { get; set; }
    public DateTime? EncerradoEm { get; set; }
    public MotivoFimCompartilhamento? MotivoEncerramento { get; set; }

    // Última posição. Fica nula antes do primeiro envio e depois de encerrar.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? PrecisaoM { get; set; }

    /// <summary>O aparelho informou que a posição vem de um app de GPS falso. Vai como aviso ao
    /// motorista; não bloqueia.</summary>
    public bool Simulada { get; set; }

    /// <summary>Quando o aparelho mediu a posição.</summary>
    public DateTime? CapturadaEm { get; set; }

    /// <summary>Quando o servidor recebeu o último envio (também depois de encerrar).</summary>
    public DateTime? RecebidaEm { get; set; }
}
