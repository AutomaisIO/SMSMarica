namespace SMSMarica.Data.Entities;

/// <summary>
/// Selo de autenticidade da Declaração de Comparecimento. O <see cref="Id"/> (UUID) é a
/// chave pública impressa no QR Code do PDF; ao ser lido, leva a uma página pública que
/// confirma a validade e mostra nome/horário/descrição. Um registro por solicitação
/// (estável). O nome do paciente NÃO é persistido aqui (vem do hub FHIR na verificação).
/// </summary>
public class DeclaracaoComparecimentoVerificacao
{
    /// <summary>Chave pública (selo) referenciada no QR Code.</summary>
    public Guid Id { get; set; }

    public Guid ExameImagemId { get; set; }
    public ExameImagem? ExameImagem { get; set; }

    /// <summary>
    /// Data/hora do exame mostrada no documento (wall-clock local, sem fuso). Snapshot
    /// para a verificação bater exatamente com o PDF impresso.
    /// </summary>
    public DateTime DataHoraExame { get; set; }

    public DateTime CriadoEm { get; set; }
}
