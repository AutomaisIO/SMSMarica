namespace SMSMais.Core.Laudos.Verificacao;

/// <summary>
/// Selo de verificação do laudo (ADR-0061): o QR Code do rodapé leva a uma página pública que
/// diz se o laudo é válido e oferece o PDF oficial para download.
/// </summary>
public interface ILaudoVerificacaoService
{
    /// <summary>
    /// Selo do laudo (cria na primeira vez; estável depois). Chamado ao gerar o PDF-base de
    /// assinatura — o código tem de estar no PDF antes de a assinatura travar os bytes.
    /// </summary>
    Task<SeloVerificacaoLaudo> ObterSeloAsync(Guid laudoId, bool assinaturaDigital, CancellationToken cancellationToken = default);

    /// <summary>Situação pública do laudo pelo código do selo; null se o código não existe.</summary>
    Task<LaudoVerificacaoPublicaDto?> VerificarAsync(Guid codigo, CancellationToken cancellationToken = default);

    /// <summary>PDF oficial (o aprovado pelo médico) pelo código do selo; null se não houver.</summary>
    Task<byte[]?> ObterPdfOficialAsync(Guid codigo, CancellationToken cancellationToken = default);
}

/// <summary>
/// O que o renderer precisa para imprimir o selo no rodapé.
/// <see cref="AssinaturaDigital"/> decide a frase: assinado com ICP-Brasil ou emitido só com
/// carimbo (médico sem certificado).
/// </summary>
public sealed record SeloVerificacaoLaudo(Guid Codigo, string Url, byte[] QrPng, bool AssinaturaDigital);

/// <summary>Dados mostrados na página pública de verificação.</summary>
/// <param name="Liberado">Há documento oficial aprovado pelo médico.</param>
/// <param name="Substituido">Existe versão mais nova do laudo (retificação); este documento foi substituído.</param>
/// <param name="AssinaturaDigital">true = assinatura ICP-Brasil; false = só carimbo (médico sem certificado).</param>
public sealed record LaudoVerificacaoPublicaDto(
    bool Liberado,
    bool Substituido,
    string PacienteNome,
    string Exame,
    string MedicoNome,
    string MedicoRegistro,
    DateTime? EmitidoEm,
    bool AssinaturaDigital,
    string? CertificadoTitular,
    string? CertificadoEmissor);
