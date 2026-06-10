namespace SMSMarica.Core.Laudos.Assinatura.Dtos;

/// <summary>Status da assinatura de um laudo (para o front fazer polling).</summary>
public sealed record AssinaturaStatusDto(
    Guid? AssinaturaId,
    string Status,
    DateTime? AssinadoEm,
    string? CertificadoTitular,
    string? Formato);

/// <summary>
/// Resultado do "iniciar": id da assinatura + a chave de uso único que o front
/// passa ao agente via <c>automais-assinador://...?chave=</c>.
/// </summary>
public sealed record IniciarAssinaturaResultado(Guid AssinaturaId, string Chave);

/// <summary>Resposta ao agente ao reivindicar o job pela chave.</summary>
public sealed record ReivindicarResultado(
    Guid AssinaturaId,
    string? CpfMedico,
    string LaudoTitulo);

/// <summary>Resposta ao agente após enviar o certificado: o hash a assinar.</summary>
public sealed record PrepararJobResultadoDto(
    string ToSignHashBase64,
    string AlgoritmoHash);

/// <summary>PDF para download + se é a versão assinada.</summary>
public sealed record PdfDownloadDto(byte[] Conteudo, bool Assinado);
