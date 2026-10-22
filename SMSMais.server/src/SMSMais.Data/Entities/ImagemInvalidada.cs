namespace SMSMais.Data.Entities;

/// <summary>
/// Imagem de um estudo PACS que a médica retirou do que vai ao paciente — o equipamento manda
/// imagens ruins junto (densitometria, raio-X: 5 enviadas, 1 laudada) e todas iam para o PDF.
///
/// <para><b>Soft delete só da ENTREGA:</b> a imagem continua no PACS e no S3, e continua visível
/// no visualizador (marcada) para quem lauda. O que muda é que os PDFs do paciente (exame completo,
/// imagens do app do cidadão e do acervo) deixam de incluí-la. Restaurar = preencher
/// <see cref="ExcluidoEm"/> desta linha; uma nova invalidação da mesma imagem cria outra linha
/// (trilha completa: quem tirou, quem devolveu, quando).</para>
///
/// <para>Chaveada pelo estudo + SOPInstanceUID (como <see cref="EstudoAnotacao"/>), não pelo exame
/// do pedido: o PDF é montado a partir do estudo real (o da worklist ou o da associação), e a
/// imagem é do estudo.</para>
/// </summary>
public class ImagemInvalidada
{
    public Guid Id { get; set; }

    /// <summary>UID do estudo DICOM (o estudo REAL, onde a imagem vive no dcm4chee).</summary>
    public string StudyInstanceUID { get; set; } = string.Empty;

    /// <summary>UID da instância (a imagem) dentro do estudo.</summary>
    public string SopInstanceUID { get; set; } = string.Empty;

    /// <summary>Motivo opcional ("imagem tremida", "posicionamento"…).</summary>
    public string? Motivo { get; set; }

    /// <summary>Quem retirou a imagem (médica que lauda ou usuário com acesso global).</summary>
    public Guid CriadoPor { get; set; }
    public Usuario? CriadoPorUsuario { get; set; }
    public DateTime CriadoEm { get; set; }

    /// <summary>Restauração: a imagem volta a ir ao paciente. Null = invalidação vigente.</summary>
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
