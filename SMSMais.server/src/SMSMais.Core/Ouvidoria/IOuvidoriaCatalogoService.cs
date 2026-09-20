using SMSMais.Core.Ouvidoria.Dtos;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Catálogo da ouvidoria (ADR-0060): pontos de resposta, assuntos, marcadores e a configuração
/// singleton. O seed dos 22 assuntos e da configuração é idempotente e roda na primeira leitura
/// (<see cref="GarantirCatalogoBaseAsync"/>), nunca em migration.
/// </summary>
public interface IOuvidoriaCatalogoService
{
    /// <summary>Semeia assuntos e configuração se ainda não existirem. Chamado por <see cref="ListarAssuntosAsync"/> e <see cref="ObterConfiguracaoAsync"/>.</summary>
    Task GarantirCatalogoBaseAsync(CancellationToken ct = default);

    // ---- Pontos de resposta ----

    /// <summary>Todos os pontos (ativos e inativos, exceto excluídos), com membros e pendentes.</summary>
    Task<IReadOnlyList<PontoRespostaDto>> ListarPontosRespostaAsync(CancellationToken ct = default);
    Task<PontoRespostaDto> ObterPontoRespostaAsync(Guid id, CancellationToken ct = default);
    Task<PontoRespostaDto> CriarPontoRespostaAsync(SalvarPontoRespostaRequest request, CancellationToken ct = default);

    /// <summary>Atualiza dados e <b>substitui</b> o conjunto de membros pelo enviado.</summary>
    Task AtualizarPontoRespostaAsync(Guid id, SalvarPontoRespostaRequest request, CancellationToken ct = default);

    /// <summary>Ids dos pontos <b>ativos</b> de que o usuário é membro (base do "meu ponto").</summary>
    Task<IReadOnlyList<Guid>> PontosDoUsuarioAsync(Guid usuarioId, CancellationToken ct = default);

    // ---- Assuntos ----

    /// <summary>Árvore plana (assunto e subassunto com <c>PaiId</c>), inclusive inativos, ordenada por pai/ordem/nome.</summary>
    Task<IReadOnlyList<AssuntoDto>> ListarAssuntosAsync(CancellationToken ct = default);
    Task<AssuntoDto> CriarAssuntoAsync(SalvarAssuntoRequest request, CancellationToken ct = default);
    Task AtualizarAssuntoAsync(Guid id, SalvarAssuntoRequest request, CancellationToken ct = default);

    // ---- Marcadores ----

    Task<IReadOnlyList<MarcadorDto>> ListarMarcadoresAsync(CancellationToken ct = default);
    Task<MarcadorDto> CriarMarcadorAsync(SalvarMarcadorRequest request, CancellationToken ct = default);
    Task AtualizarMarcadorAsync(Guid id, SalvarMarcadorRequest request, CancellationToken ct = default);

    // ---- Configuração ----

    Task<OuvidoriaConfiguracaoDto> ObterConfiguracaoAsync(CancellationToken ct = default);
    Task SalvarConfiguracaoAsync(OuvidoriaConfiguracaoDto request, CancellationToken ct = default);
}
