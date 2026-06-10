namespace Automais.Assinador.Core.Pades;

/// <summary>Dados do carimbo visual fixo aplicado ao PDF assinado.</summary>
public sealed record CarimboVisual(
    string NomeMedico,
    string Crm,
    string UfCrm,
    string? Rqe,
    string TextoRodape);

/// <summary>Entrada do passo "preparar": PDF original + cadeia do certificado do signatário.</summary>
public sealed record PreparacaoRequisicao(
    byte[] Pdf,
    IReadOnlyList<byte[]> CadeiaCertificado,
    CarimboVisual Visual);

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
