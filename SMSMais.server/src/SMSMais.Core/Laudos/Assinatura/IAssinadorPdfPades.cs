namespace SMSMais.Core.Laudos.Assinatura;

/// <summary>
/// Abstração da biblioteca de assinatura PAdES (Lacuna PKI SDK / iText / etc.).
/// É o ÚNICO ponto que conhece a lib concreta — trocar de fornecedor mexe só na
/// implementação, não no orquestrador, controller, persistência nem no front.
///
/// Modela a assinatura diferida (two-step), em que a chave privada vive no
/// cliente (Web PKI / VIDaaS Connect) e o documento nunca sai do servidor:
///   1. <see cref="PrepararAsync"/> — prepara o PDF (placeholder/ByteRange) e
///      devolve o hash a ser assinado + um estado opaco de transferência.
///   2. cliente assina o hash (Web PKI dispara o VIDaaS Connect).
///   3. <see cref="ConcluirAsync"/> — embute o CMS no PDF e devolve o assinado.
/// </summary>
public interface IAssinadorPdfPades
{
    /// <summary>
    /// Prepara <paramref name="pdfOriginal"/> para assinatura, aplicando o carimbo
    /// visual em posição fixa. Precisa da <paramref name="cadeiaCertificado"/> (DER do
    /// signatário + intermediários) para montar os atributos CMS. Retorna o hash a ser
    /// assinado pelo cliente e o estado de transferência devolvido em <see cref="ConcluirAsync"/>.
    /// </summary>
    Task<PreparacaoAssinatura> PrepararAsync(
        byte[] pdfOriginal,
        IReadOnlyList<byte[]> cadeiaCertificado,
        DadosVisualAssinatura visual,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Conclui a assinatura: embute <paramref name="assinaturaCliente"/> (assinatura
    /// crua/CMS produzida pelo certificado) no PDF preparado em
    /// <paramref name="transferState"/> e devolve o PDF assinado + dados do
    /// certificado signatário (titular, emissor, CPF) extraídos do CMS.
    /// </summary>
    Task<ResultadoAssinatura> ConcluirAsync(
        byte[] transferState,
        byte[] assinaturaCliente,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Dados do carimbo visual aplicado ao PDF assinado. <see cref="CarimboPngBase64"/>
/// é a imagem do carimbo já composta pelo servidor (rubrica + identificação no
/// quadrado virtual); quando presente, é estampada graphic-only. Os campos de texto
/// ficam como fallback (médico sem rubrica / composição indisponível).
/// </summary>
public sealed record DadosVisualAssinatura(
    string NomeMedico,
    string Crm,
    string UfCrm,
    string? Rqe,
    string TextoRodape,
    string? CarimboPngBase64 = null,
    CarimboPosicaoPdf? Posicao = null);

/// <summary>
/// Posição do carimbo no PDF, em pontos (origem inferior-esquerda; página 1-based),
/// escolhida pela médica no painel (ADR-0049). Nula = padrão legado (rodapé da última
/// página) — mantém o comportamento anterior para quem não posiciona.
/// </summary>
public sealed record CarimboPosicaoPdf(int Pagina, double X, double Y, double Largura, double Altura);

/// <summary>Resultado do passo "preparar": o que o cliente precisa assinar + estado opaco.</summary>
public sealed record PreparacaoAssinatura(
    byte[] ToSignHash,
    string AlgoritmoHash,
    byte[] TransferState);

/// <summary>Resultado do passo "concluir": PDF assinado + identidade do certificado.</summary>
public sealed record ResultadoAssinatura(
    byte[] PdfAssinado,
    string Formato,
    string? CertificadoTitular,
    string? CertificadoEmissor,
    string? CpfTitular);
