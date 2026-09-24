using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Laudos.Assinatura.Dtos;

/// <summary>Status da assinatura de um laudo (para o front fazer polling).</summary>
public sealed record AssinaturaStatusDto(
    Guid? AssinaturaId,
    string Status,
    DateTime? AssinadoEm,
    string? CertificadoTitular,
    string? Formato,
    // Caminho do job (ADR-0061); null nos jobs anteriores ao ADR (todos Desktop).
    ModoAssinaturaMedico? Modo = null);

/// <summary>
/// Resultado do "iniciar". O que o front faz em seguida depende do <see cref="Modo"/>
/// (ADR-0061):
/// <list type="bullet">
/// <item><b>Desktop</b>: lança o agente com a <see cref="Chave"/> de uso único via
/// <c>automais-assinador://...?chave=</c>.</item>
/// <item><b>Nuvem</b>: abre a <see cref="UrlAutorizacao"/>, onde o médico aprova no app VIDaaS.</item>
/// <item><b>SemCertificado</b>: nada — o carimbo já foi aplicado e o job está aguardando a conferência.</item>
/// </list>
/// </summary>
public sealed record IniciarAssinaturaResultado(
    Guid AssinaturaId,
    string? Chave,
    ModoAssinaturaMedico Modo = ModoAssinaturaMedico.Desktop,
    string? UrlAutorizacao = null);

/// <summary>
/// Posição do carimbo escolhida pela médica no painel (ADR-0049), em pontos PDF
/// (origem inferior-esquerda; <see cref="Pagina"/> 1-based). Enviada ao "iniciar".
/// </summary>
public sealed record CarimboPosicaoDto(int Pagina, double X, double Y, double Largura, double Altura);

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
