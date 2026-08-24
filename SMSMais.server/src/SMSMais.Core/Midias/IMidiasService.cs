using SMSMais.Core.Midias.Dtos;

namespace SMSMais.Core.Midias;

public interface IMidiasService
{
    /// <summary>
    /// Guarda um binário. Se já existir uma mídia com o mesmo conteúdo (hash
    /// SHA-256), reaproveita o registro existente em vez de duplicar.
    /// </summary>
    Task<MidiaDto> EnviarAsync(
        Guid? usuarioId,
        string nomeArquivo,
        string mimeType,
        byte[] conteudo,
        string? categoria,
        CancellationToken cancellationToken = default);

    /// <summary>Carrega o binário para servir (ou null se não existe).</summary>
    Task<MidiaConteudo?> ObterConteudoAsync(Guid id, CancellationToken cancellationToken = default);
}
