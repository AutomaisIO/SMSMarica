using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Comum;

/// <summary>
/// Quais solicitações este usuário enxerga — a mesma pergunta do <see cref="EscopoUnidade"/>,
/// mais a ampliação do agente regulador (ADR-0052).
///
/// <para><b>Por que não dá para usar o <c>EscopoUnidade</c> direto:</b> ele responde "quais
/// unidades", e o agente regulador não é definido por unidade nenhuma — ele existe justamente
/// para ver a fila do município inteiro. Sem esta camada, ou o agente ficaria preso à unidade em
/// que está lotado (e a fila global não funcionaria), ou o escopo teria de ser afrouxado para
/// todo mundo.</para>
///
/// <para><b>O agente vê o município, mas só as filas do seu perfil</b> (pedido do Bernardo,
/// 22/10/2026): além do módulo 48, cada fila tem módulo próprio — NAR
/// (<see cref="ModuloPermissao.RegulacaoFilaNar"/>), Internas
/// (<see cref="ModuloPermissao.RegulacaoFilaInterna"/>) e Intermunicipais
/// (<see cref="ModuloPermissao.RegulacaoFilaIntermunicipal"/>). Fora delas, o agente é um usuário
/// comum: vê só o que é das unidades a que está vinculado.</para>
///
/// <para>O resto continua valendo: quem não tem o módulo 48 vê só as unidades a que está
/// vinculado, e quem não tem vínculo nenhum <b>não vê nada</b> (fail-closed, ADR-0037).</para>
/// </summary>
public interface IRegulacaoEscopo
{
    Task<EscopoUnidadeResultado> ResolverAsync(CancellationToken ct);

    /// <summary>O usuário atual é agente regulador (módulo 48)?</summary>
    Task<bool> EhAgenteAsync(CancellationToken ct);

    /// <summary>
    /// As filas que o agente enxerga na Gestão de fila. Vazio para quem não é agente — e para o
    /// agente cujo perfil não tem nenhuma das três (fail-closed). Job, sem usuário, vê todas.
    /// </summary>
    Task<IReadOnlySet<FluxoRegulacao>> FluxosDoAgenteAsync(CancellationToken ct);

    /// <summary>É agente E o perfil dele alcança a fila deste pedido?</summary>
    Task<bool> EhAgenteDoCasoAsync(RegulacaoSolicitacao s, CancellationToken ct);

    /// <summary>
    /// Recusa quem não é agente. Usado nas ações da triagem — o atributo do controller já
    /// exige o módulo, mas serviço chamado por outro caminho (job, outro service) não passa
    /// por atributo nenhum.
    /// </summary>
    Task ExigirAgenteAsync(CancellationToken ct);

    /// <summary>
    /// <see cref="ExigirAgenteAsync"/> para UM pedido: além do módulo 48, o perfil precisa da fila
    /// dele. É o que impede o técnico da fila interna de assumir ou registrar um NAR.
    /// </summary>
    Task ExigirAgenteDoCasoAsync(RegulacaoSolicitacao s, CancellationToken ct);

    /// <summary>
    /// Recusa, como "não encontrado", a solicitação que o usuário atual não alcança: unidade fora
    /// do escopo, fila fora do perfil do agente, ou <b>rascunho de outra pessoa</b>. É a mesma
    /// pergunta para o detalhe, a linha do tempo, as regras e os anexos — e mora aqui para as
    /// quatro portas não divergirem.
    /// </summary>
    Task ExigirAlcanceAsync(RegulacaoSolicitacao s, CancellationToken ct);
}

/// <inheritdoc cref="IRegulacaoEscopo"/>
public sealed class RegulacaoEscopo(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IIdentidadeService identidade) : IRegulacaoEscopo
{
    /// <summary>Que módulo abre qual fila. "Intermunicipais" é o fluxo Externo (SER, SERNIT, ESUS).</summary>
    public static readonly IReadOnlyDictionary<FluxoRegulacao, ModuloPermissao> ModuloDaFila =
        new Dictionary<FluxoRegulacao, ModuloPermissao>
        {
            [FluxoRegulacao.Nar] = ModuloPermissao.RegulacaoFilaNar,
            [FluxoRegulacao.Interno] = ModuloPermissao.RegulacaoFilaInterna,
            [FluxoRegulacao.Externo] = ModuloPermissao.RegulacaoFilaIntermunicipal,
        };

    private static readonly IReadOnlySet<FluxoRegulacao> TodasAsFilas = ModuloDaFila.Keys.ToHashSet();

    /// <summary>As permissões resolvidas são lidas uma vez por requisição: o escopo é scoped e
    /// cada tela pergunta "é agente?" e "quais filas?" várias vezes.</summary>
    private IReadOnlySet<ModuloPermissao>? modulos;

    public async Task<EscopoUnidadeResultado> ResolverAsync(CancellationToken ct)
    {
        if (await EhAgenteAsync(ct)) return EscopoUnidadeResultado.Tudo;
        return await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
    }

    public async Task<bool> EhAgenteAsync(CancellationToken ct)
    {
        // Sem usuário no contexto é job/serviço, que vê tudo — mesma porta do EscopoUnidade.
        if (usuarioAtual.UsuarioId is null) return true;
        return (await ModulosAsync(ct)).Contains(ModuloPermissao.RegulacaoTriagem);
    }

    public async Task<IReadOnlySet<FluxoRegulacao>> FluxosDoAgenteAsync(CancellationToken ct)
    {
        if (usuarioAtual.UsuarioId is null) return TodasAsFilas;

        var meus = await ModulosAsync(ct);
        if (!meus.Contains(ModuloPermissao.RegulacaoTriagem)) return new HashSet<FluxoRegulacao>();

        return ModuloDaFila.Where(f => meus.Contains(f.Value)).Select(f => f.Key).ToHashSet();
    }

    public async Task<bool> EhAgenteDoCasoAsync(RegulacaoSolicitacao s, CancellationToken ct)
    {
        var fluxos = await FluxosDoAgenteAsync(ct);
        if (fluxos.Contains(s.Fluxo)) return true;
        if (fluxos.Count == 0) return false;

        // Compartilhado com uma fila do perfil também conta.
        var minhas = fluxos.ToArray();
        return await db.RegulacaoSolicitacaoCompartilhamentos.AsNoTracking()
            .AnyAsync(c => c.SolicitacaoId == s.Id && minhas.Contains(c.Fila), ct);
    }

    /// <summary>O pedido está numa destas filas — pela origem ou por compartilhamento.</summary>
    public static Expression<Func<RegulacaoSolicitacao, bool>> NasFilas(FluxoRegulacao[] fluxos) =>
        s => fluxos.Contains(s.Fluxo) || s.Compartilhamentos.Any(c => fluxos.Contains(c.Fila));

    public async Task ExigirAgenteAsync(CancellationToken ct)
    {
        if (!await EhAgenteAsync(ct))
        {
            throw new UnauthorizedAccessException(
                "Esta ação é do agente regulador (módulo Regulação — Gestão de fila).");
        }
    }

    public async Task ExigirAgenteDoCasoAsync(RegulacaoSolicitacao s, CancellationToken ct)
    {
        await ExigirAgenteAsync(ct);
        if (!await EhAgenteDoCasoAsync(s, ct))
        {
            throw new UnauthorizedAccessException(
                $"Seu perfil não tem acesso à fila {NomeDaFila(s.Fluxo)} da Gestão de fila.");
        }
    }

    public async Task ExigirAlcanceAsync(RegulacaoSolicitacao s, CancellationToken ct)
    {
        // Rascunho é trabalho em andamento de uma pessoa: nem o colega da mesma unidade nem o
        // agente o enxergam. "Não encontrado" e não "sem permissão" — dizer que existe já vaza
        // que aquele paciente tem um pedido sendo montado.
        if (!RascunhoVisivel(s.Status, s.CriadoPorUsuarioId, usuarioAtual.UsuarioId))
        {
            throw new NaoEncontradoException("Solicitação da regulação", s.Id);
        }

        // O agente alcança o município nas filas do perfil; fora delas, vale o vínculo com a unidade.
        if (await EhAgenteDoCasoAsync(s, ct)) return;

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        if (!escopo.VeTudo && !escopo.Unidades.Contains(s.UnidadeSolicitanteId))
        {
            throw new NaoEncontradoException("Solicitação da regulação", s.Id);
        }
    }

    /// <summary>
    /// Rascunho só para quem o abriu. Sem usuário no contexto (job, importação) não há a quem
    /// restringir — mesma porta aberta do <see cref="EscopoUnidade"/>.
    /// </summary>
    public static bool RascunhoVisivel(StatusRegulacao status, Guid criadoPor, Guid? usuarioId) =>
        status != StatusRegulacao.Rascunho || usuarioId is null || criadoPor == usuarioId;

    public static string NomeDaFila(FluxoRegulacao fluxo) => fluxo switch
    {
        FluxoRegulacao.Nar => "NAR",
        FluxoRegulacao.Interno => "Internas (SISREG)",
        FluxoRegulacao.Externo => "Intermunicipais",
        _ => fluxo.ToString(),
    };

    private async Task<IReadOnlySet<ModuloPermissao>> ModulosAsync(CancellationToken ct)
    {
        if (modulos is not null) return modulos;
        try
        {
            var permissoes = await identidade.ObterPermissoesResolvidasAsync(usuarioAtual.UsuarioId!.Value, ct);
            modulos = permissoes.Resolvidas
                .Where(p => p.Acoes != AcoesPermissao.Nenhuma)
                .Select(p => p.Modulo)
                .ToHashSet();
        }
        catch (NaoEncontradoException)
        {
            // Usuário sumiu entre o token e a consulta: não é agente. Fail-closed.
            modulos = new HashSet<ModuloPermissao>();
        }
        return modulos;
    }
}
