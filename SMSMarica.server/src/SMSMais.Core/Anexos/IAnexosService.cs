using SMSMais.Core.Anexos.Dtos;

namespace SMSMais.Core.Anexos;

/// <summary>
/// Anexos de exame (ponte QR → PWA "Arquivos Saúde Maricá"). O médico gera um
/// token de upload na tela de Anamnese; o cidadão digitaliza o documento no
/// celular (PWA anônimo) e o envia; o médico revisa e salva. Documentos salvos
/// entram no histórico do paciente.
/// </summary>
public interface IAnexosService
{
    /// <summary>Cria um token de upload (escopo de 1 solicitação) e devolve a URL para o QR.</summary>
    Task<CriarTokenRespostaDto> CriarTokenAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>Valida um token (lado PWA): existe, não revogado, não expirado. Lança se inválido.</summary>
    Task<ValidarTokenRespostaDto> ValidarTokenAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recebe um upload (lado PWA, anônimo): cria o <c>DocumentoExame</c> (Pendente,
    /// origem <c>pwa-scanner</c>), grava o binário no armazenamento e atualiza o uso do token.
    /// Dedup por SHA-256 dentro da mesma solicitação. Não consome o token (multi-uso no TTL).
    /// </summary>
    Task<AnexoUploadRespostaDto> ReceberUploadAsync(
        string token,
        string nome,
        string? descricao,
        int? paginas,
        byte[] conteudo,
        string mimeType,
        CancellationToken cancellationToken = default);

    /// <summary>Lista os documentos de uma solicitação (todos os status, não-excluídos).</summary>
    Task<IReadOnlyList<AnexoExameDto>> ListarPorSolicitacaoAsync(
        Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>Confirma/atualiza um documento (Pendente → Salvo), opcionalmente ajustando nome/descrição.</summary>
    Task<AnexoExameDto> SalvarAsync(Guid id, SalvarAnexoDto dto, CancellationToken cancellationToken = default);

    /// <summary>Exclusão lógica (soft delete) de um documento.</summary>
    Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Carrega o binário de um documento para servir (ou <c>null</c> se não existe).</summary>
    Task<AnexoConteudo?> ObterConteudoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Histórico do paciente: documentos Salvos, agregados via SolicitacaoExame.PacienteId.</summary>
    Task<IReadOnlyList<AnexoExameDto>> ListarPorPacienteAsync(
        Guid pacienteId, CancellationToken cancellationToken = default);
}
