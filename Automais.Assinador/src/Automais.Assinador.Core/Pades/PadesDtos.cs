namespace Automais.Assinador.Core.Pades;

/// <summary>
/// Dados do carimbo visual aplicado ao PDF assinado. <see cref="CarimboPng"/> é a
/// imagem do carimbo já composta pelo chamador (rubrica + identificação); quando
/// presente, é estampada graphic-only. Os campos de texto são fallback.
/// </summary>
public sealed record CarimboVisual(
    string NomeMedico,
    string Crm,
    string UfCrm,
    string? Rqe,
    string TextoRodape,
    byte[]? CarimboPng = null);

/// <summary>
/// Posição do carimbo/assinatura no PDF, em pontos PDF (origem inferior-esquerda),
/// escolhida pela médica no painel. <see cref="Pagina"/> é 1-based (igual iText).
/// Quando nula em <see cref="PreparacaoRequisicao"/>, o assinador mantém o padrão
/// legado (quadrado centralizado no rodapé da última página).
/// </summary>
public sealed record CarimboPosicao(int Pagina, float X, float Y, float Largura, float Altura);

/// <summary>Entrada do passo "preparar": PDF original + cadeia do certificado do signatário.</summary>
public sealed record PreparacaoRequisicao(
    byte[] Pdf,
    IReadOnlyList<byte[]> CadeiaCertificado,
    CarimboVisual Visual,
    CarimboPosicao? Posicao = null);

/// <summary>
/// Saída do passo "preparar": o hash que o cliente (agente/VIDaaS) deve assinar +
/// um estado de transferência opaco que volta no passo "concluir".
/// </summary>
public sealed record PreparacaoResultado(
    byte[] ToSignHash,
    string AlgoritmoHash,
    byte[] TransferState);

/// <summary>Entrada do passo "concluir": o estado opaco + a assinatura crua produzida pela chave.</summary>
public sealed record ConclusaoRequisicao(
    byte[] TransferState,
    byte[] RawSignature);

/// <summary>Saída do passo "concluir": PDF assinado + identidade do certificado signatário.</summary>
public sealed record ConclusaoResultado(
    byte[] PdfAssinado,
    string Formato,
    string? CertificadoTitular,
    string? CertificadoEmissor,
    string? CpfTitular);
