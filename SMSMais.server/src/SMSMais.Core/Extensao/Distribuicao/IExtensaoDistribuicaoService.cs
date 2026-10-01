using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Extensao.Distribuicao;

/// <summary>
/// Distribuição da extensão do Chrome e do atualizador pela plataforma (ADR-0064). Três públicos:
/// quem está logado no painel (baixa o instalador, autoriza um computador), o computador (pede
/// autorização, consulta versão, baixa pacote — com o token dele) e quem administra (computadores
/// e versões publicadas).
/// </summary>
public interface IExtensaoDistribuicaoService
{
    // --- painel: qualquer usuário logado
    Task<SituacaoDistribuicaoDto> ObterSituacaoAsync(CancellationToken ct = default);

    /// <summary>
    /// O instalador para quem está logado: o atualizador de produção + o endereço da API e um
    /// código de ativação de uso único, já autorizado por quem baixou.
    /// </summary>
    Task<ArquivoPublicado> GerarInstaladorAsync(string apiBase, CancellationToken ct = default);

    Task<AtivacaoPendenteDto> ObterAtivacaoAsync(string codigoPublico, CancellationToken ct = default);
    Task<AtivacaoPendenteDto> AutorizarAtivacaoAsync(string codigoPublico, CancellationToken ct = default);

    // --- computador: sem login
    Task<AtivacaoIniciadaDto> IniciarAtivacaoAsync(IniciarAtivacaoRequest request, CancellationToken ct = default);
    Task<TrocaDeCodigo> TrocarCodigoAsync(TrocarCodigoRequest request, CancellationToken ct = default);

    // --- computador: com o token dele
    /// <summary>Nulo = token desconhecido ou computador revogado.</summary>
    Task<DispositivoAutenticado?> AutenticarAsync(string? token, CancellationToken ct = default);

    /// <summary>Registra o contato (inventário) e devolve a versão que este computador deve ter. Nulo = nada publicado.</summary>
    Task<VersaoDaExtensaoDto?> ConsultarExtensaoAsync(
        DispositivoAutenticado dispositivo, InventarioDoComputador inventario, CancellationToken ct = default);

    Task<VersaoDoAtualizadorDto?> ConsultarAtualizadorAsync(DispositivoAutenticado dispositivo, CancellationToken ct = default);
    Task<ArquivoPublicado?> BaixarAsync(DispositivoAutenticado dispositivo, ExtensaoArtefato artefato, CancellationToken ct = default);

    // --- administração
    Task<IReadOnlyList<DispositivoDto>> ListarDispositivosAsync(CancellationToken ct = default);
    Task<DispositivoDto> DefinirCanalAsync(Guid id, ExtensaoCanal canal, CancellationToken ct = default);
    Task<DispositivoDto> RevogarAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<PacoteDto>> ListarPacotesAsync(CancellationToken ct = default);

    /// <summary>
    /// Publica no canal de teste. Para a extensão a versão é lida do manifest; para o atualizador, é
    /// informada. <paramref name="pelaApi"/>: veio pela API de publicação (com a chave).
    /// </summary>
    Task<PacoteDto> PublicarAsync(
        ExtensaoArtefato artefato, string? versao, string? notas, byte[] conteudo,
        bool pelaApi = false, CancellationToken ct = default);

    Task<PacoteDto> PromoverAsync(Guid id, bool pelaApi = false, CancellationToken ct = default);
    Task<PacoteDto> RetirarAsync(Guid id, CancellationToken ct = default);

    // --- API de publicação: uma opção da tela (chave gerada e revogada por quem administra)
    Task<ChavePublicacaoDto> ObterChavePublicacaoAsync(CancellationToken ct = default);

    /// <summary>Gera a chave (revogando a anterior) e a devolve em claro — é a única vez em que aparece.</summary>
    Task<ChaveGeradaDto> GerarChavePublicacaoAsync(CancellationToken ct = default);

    Task<ChavePublicacaoDto> RevogarChavePublicacaoAsync(CancellationToken ct = default);

    /// <summary>A chave apresentada vale? (registra o uso).</summary>
    Task<bool> ChavePublicacaoValeAsync(string? chave, CancellationToken ct = default);
}
