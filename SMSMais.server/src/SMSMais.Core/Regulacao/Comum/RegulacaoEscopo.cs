using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

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
/// <para>O resto continua valendo: quem não tem o módulo 48 vê só as unidades a que está
/// vinculado, e quem não tem vínculo nenhum <b>não vê nada</b> (fail-closed, ADR-0037).</para>
/// </summary>
public interface IRegulacaoEscopo
{
    Task<EscopoUnidadeResultado> ResolverAsync(CancellationToken ct);

    /// <summary>O usuário atual é agente regulador (módulo 48)?</summary>
    Task<bool> EhAgenteAsync(CancellationToken ct);

    /// <summary>
    /// Recusa quem não é agente. Usado nas ações da triagem — o atributo do controller já
    /// exige o módulo, mas serviço chamado por outro caminho (job, outro service) não passa
    /// por atributo nenhum.
    /// </summary>
    Task ExigirAgenteAsync(CancellationToken ct);
}

/// <inheritdoc cref="IRegulacaoEscopo"/>
public sealed class RegulacaoEscopo(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IIdentidadeService identidade) : IRegulacaoEscopo
{
    public async Task<EscopoUnidadeResultado> ResolverAsync(CancellationToken ct)
    {
        if (await EhAgenteAsync(ct)) return EscopoUnidadeResultado.Tudo;
        return await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
    }

    public async Task<bool> EhAgenteAsync(CancellationToken ct)
    {
        var usuarioId = usuarioAtual.UsuarioId;

        // Sem usuário no contexto é job/serviço, que vê tudo — mesma porta do EscopoUnidade.
        if (usuarioId is null) return true;

        try
        {
            var permissoes = await identidade.ObterPermissoesResolvidasAsync(usuarioId.Value, ct);
            return permissoes.Resolvidas.Any(p =>
                p.Modulo == ModuloPermissao.RegulacaoTriagem && p.Acoes != AcoesPermissao.Nenhuma);
        }
        catch (NaoEncontradoException)
        {
            // Usuário sumiu entre o token e a consulta: não é agente. Fail-closed.
            return false;
        }
    }

    public async Task ExigirAgenteAsync(CancellationToken ct)
    {
        if (!await EhAgenteAsync(ct))
        {
            throw new UnauthorizedAccessException(
                "Esta ação é do agente regulador (módulo Regulação — Agente regulador).");
        }
    }
}
