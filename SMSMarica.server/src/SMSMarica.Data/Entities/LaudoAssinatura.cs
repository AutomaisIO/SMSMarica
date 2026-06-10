using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Artefato imutável de assinatura digital ICP-Brasil (PAdES) de um
/// <see cref="Laudo"/> finalizado. Não altera o laudo (que segue append-only):
/// a assinatura é um fato adicional, registrado uma única vez por laudo.
///
/// O PDF assinado é byte-estável — depois de <see cref="StatusAssinatura.Concluida"/>
/// ele é servido sempre a partir de <see cref="PdfAssinado"/>, nunca re-renderizado
/// (regerar quebraria a assinatura).
/// </summary>
public class LaudoAssinatura
{
    public Guid Id { get; set; }

    /// <summary>Laudo assinado (FK → laudo.id). Único quando <see cref="StatusAssinatura.Concluida"/>.</summary>
    public Guid LaudoId { get; set; }
    public Laudo? Laudo { get; set; }

    /// <summary>Médico (Practitioner do hub) dono do job. A identidade vem da sessão web autenticada.</summary>
    public Guid MedicoId { get; set; }

    /// <summary>
    /// Chave aleatória de uso único entregue ao agente via protocolo
    /// <c>automais-assinador://...?chave=</c>. É a autorização do agente (substitui
    /// device token): o agente apresenta a chave para reivindicar/preparar/concluir.
    /// Limpa ao concluir/falhar.
    /// </summary>
    public string? ChaveAgente { get; set; }

    /// <summary>Expiração da <see cref="ChaveAgente"/> (curta — minutos).</summary>
    public DateTime? ChaveExpiraEm { get; set; }

    public StatusAssinatura Status { get; set; } = StatusAssinatura.Iniciada;

    /// <summary>PDF final com a assinatura PAdES embutida. Só preenchido quando concluída.</summary>
    public byte[]? PdfAssinado { get; set; }

    /// <summary>SHA-256 do <see cref="PdfAssinado"/> (auditoria/integridade).</summary>
    public byte[]? PdfHashSha256 { get; set; }

    /// <summary>
    /// Estado de transferência opaco do passo "preparar" (PDF preparado + ByteRange),
    /// consumido pelo passo "concluir". Transitório — limpo ao concluir/cancelar.
    /// </summary>
    public byte[]? TransferState { get; set; }

    /// <summary>Hash que o agente deve assinar (definido ao preparar). Transitório.</summary>
    public byte[]? HashParaAssinar { get; set; }

    /// <summary>Thumbprint do certificado escolhido pelo agente (auditoria).</summary>
    public string? CertThumbprint { get; set; }

    /// <summary>Quando o agente reivindicou o job e enviou o certificado (preparar).</summary>
    public DateTime? EntregueEm { get; set; }

    /// <summary>CPF do titular do certificado que assinou (só dígitos). Validado contra o médico autor.</summary>
    public string? AssinadoPorCpf { get; set; }

    /// <summary>Usuário logado que disparou a assinatura.</summary>
    public Guid? AssinadoPorUsuarioId { get; set; }

    /// <summary>CN do certificado signatário (ex.: "CLAUDIA FREIXO SEIXAS IOCKEN:05188144751").</summary>
    public string? CertificadoTitular { get; set; }

    /// <summary>Emissor da cadeia (ex.: "AC VALID RFB v5").</summary>
    public string? CertificadoEmissor { get; set; }

    /// <summary>Formato PAdES aplicado (ex.: "PAdES_AD_RB", "PAdES_AD_RT").</summary>
    public string? Formato { get; set; }

    /// <summary>True quando há carimbo de tempo (AD_RT / LTV).</summary>
    public bool ComCarimboTempo { get; set; }

    public DateTime? AssinadoEm { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Concorrência otimista via PG xmin.</summary>
    public uint RowVersion { get; set; }
}
