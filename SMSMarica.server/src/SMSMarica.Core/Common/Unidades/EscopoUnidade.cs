using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;

namespace SMSMarica.Core.Common.Unidades;

/// <summary>
/// Multitenancy por unidade: "quais unidades este usuário enxerga agora?" — resposta única,
/// independente da entidade que será filtrada.
///
/// A mesma cascata estava copiada em <c>SolicitacoesExameService</c>, <c>ConsultasService</c> e
/// <c>LaudosService</c>, e o painel de início seria a quarta cópia. Cada divergência entre elas
/// vira um vazamento de dado entre unidades que ninguém percebe — por isso a resolução mora aqui.
///
/// O que NÃO mora aqui é o <c>.Where</c>: cada entidade chega à unidade por um caminho diferente
/// (<c>ExameImagem</c> navega por <c>Solicitacao</c>, <c>Laudo</c> por outro), e generalizar o
/// filtro por expressão faria o EF trabalhar contra nós sem ganho. O chamador aplica o seu filtro
/// usando <see cref="EscopoUnidadeResultado"/>.
///
/// <b>Fail-CLOSED</b> (ADR-0037): usuário autenticado sem nenhum vínculo em <c>usuario_unidade</c>
/// e sem acesso global <b>não vê nada</b>. Até 2026-07-30 a postura era o inverso — quem não tinha
/// vínculo enxergava a rede inteira —, o que fazia da ausência de configuração a permissão mais
/// ampla do sistema. Ver ADR-0033 (extração) e ADR-0037 (a inversão).
/// </summary>
public static class EscopoUnidade
{
    /// <summary>
    /// Resolve o escopo do usuário atual. A cascata, na ordem:
    /// <list type="number">
    /// <item>sem usuário no contexto (background/serviço/token de API) ⇒ vê tudo;</item>
    /// <item>acesso global + unidade ativa válida ⇒ filtra por ela (conveniência, não restrição);</item>
    /// <item>acesso global sem ativa ⇒ vê tudo;</item>
    /// <item><b>sem nenhum vínculo ⇒ NÃO VÊ NADA</b> (fail-closed, ADR-0037);</item>
    /// <item>ativa entre os vínculos ⇒ filtra por ela;</item>
    /// <item>senão ⇒ o conjunto dos vínculos, sem unidade de referência.</item>
    /// </list>
    /// O passo 1 continua aberto de propósito: sem <c>HttpContext</c> não há a quem restringir, e
    /// fechá-lo quebraria a importação SISREG, os jobs de sincronismo e o worker de comunicação —
    /// que rodam sem usuário e precisam enxergar a rede inteira.
    /// </summary>
    public static async Task<EscopoUnidadeResultado> ResolverAsync(
        SmsMaricaDbContext db, IUsuarioAtualAccessor usuarioAtual, CancellationToken ct = default)
    {
        var usuarioId = usuarioAtual.UsuarioId;
        if (usuarioId is null) return EscopoUnidadeResultado.Tudo;

        var ativa = usuarioAtual.UnidadeAtivaId;

        // Acesso global: vínculo implícito a TODAS as unidades — a ativa (se válida) vira filtro
        // de conveniência, não restrição de segurança.
        if (await AcessoGlobalUsuario.TemAsync(db, usuarioId, ct))
        {
            return ativa.HasValue &&
                   await db.Unidades.AsNoTracking().AnyAsync(u => u.Id == ativa.Value && u.Ativo, ct)
                ? EscopoUnidadeResultado.Uma(ativa.Value)
                : EscopoUnidadeResultado.Tudo;
        }

        var vinculos = await db.UsuarioUnidades.AsNoTracking()
            .Where(v => v.UsuarioId == usuarioId && v.Unidade!.Ativo)
            .Select(v => v.UnidadeId)
            .ToArrayAsync(ct);

        // Fail-closed (ADR-0037): usuário autenticado sem nenhum vínculo NÃO VÊ NADA. Antes via a
        // rede inteira — a ausência de configuração era a permissão mais ampla do sistema.
        if (vinculos.Length == 0) return EscopoUnidadeResultado.Nada;

        return ativa.HasValue && vinculos.Contains(ativa.Value)
            ? EscopoUnidadeResultado.Uma(ativa.Value)
            : new EscopoUnidadeResultado(VeTudo: false, Unidades: vinculos, Referencia: null);
    }
}

/// <param name="VeTudo">
/// Sem restrição por unidade — o chamador não deve aplicar filtro nenhum. Verdade para acesso
/// global e para execução sem usuário (jobs). <b>Não</b> é mais verdade para quem não tem vínculo.
/// </param>
/// <param name="Unidades">
/// Unidades visíveis quando <paramref name="VeTudo"/> é <c>false</c>. Vazio significa coisas
/// opostas conforme <paramref name="VeTudo"/>: com ele <c>true</c>, "tudo"; com <c>false</c>,
/// <b>nada</b> — use <see cref="EscopoUnidadeResultado.SemAcesso"/> em vez de checar o tamanho.
/// </param>
/// <param name="Referencia">
/// A unidade "de referência" — a ativa resolvida. Define a direção da solicitação (executante ⇒
/// Recebida, solicitante ⇒ Enviada). <c>null</c> na visão do conjunto: sem uma unidade única não
/// há seta que faça sentido.
/// </param>
public sealed record EscopoUnidadeResultado(bool VeTudo, Guid[] Unidades, Guid? Referencia)
{
    public static readonly EscopoUnidadeResultado Tudo = new(true, [], null);

    /// <summary>Nenhuma unidade visível — o chamador deve devolver conjunto vazio (ADR-0037).</summary>
    public static readonly EscopoUnidadeResultado Nada = new(false, [], null);

    /// <summary>Sem acesso a unidade alguma. Distingue-se de <see cref="Tudo"/> pelo <c>VeTudo</c>.</summary>
    public bool SemAcesso => !VeTudo && Unidades.Length == 0;

    /// <summary>
    /// Escopo de uma unidade só — a ativa. Vale tanto para quem tem acesso global (a ativa é
    /// conveniência) quanto para quem só tem vínculo com ela: o resultado é o mesmo filtro.
    /// </summary>
    public static EscopoUnidadeResultado Uma(Guid unidadeId) =>
        new(VeTudo: false, Unidades: [unidadeId], Referencia: unidadeId);

    /// <summary>Filtro por unidade única — permite ao chamador usar <c>==</c> em vez de <c>IN</c>.</summary>
    public Guid? UnidadeUnica => !VeTudo && Unidades.Length == 1 ? Unidades[0] : null;
}

/// <summary>
/// Aplica o escopo de unidade sobre a espinha <c>Solicitacao</c> — a única entidade cujo filtro é
/// idêntico entre consumidores (casa EXECUTANTE ou SOLICITANTE). Entidades que chegam à unidade por
/// navegação (<c>ExameImagem</c>, <c>Laudo</c>) aplicam o seu próprio <c>.Where</c> a partir do
/// <see cref="EscopoUnidadeResultado"/>.
/// </summary>
public static class SolicitacaoNoEscopo
{
    public static IQueryable<Data.Entities.Solicitacao> Filtrar(
        IQueryable<Data.Entities.Solicitacao> query, EscopoUnidadeResultado escopo)
    {
        if (escopo.VeTudo) return query;

        // Sem acesso a unidade alguma: conjunto vazio, explícito. Deixar cair no Contains de um
        // array vazio funcionaria, mas o intent tem de ficar legível no código.
        if (escopo.SemAcesso) return query.Where(_ => false);

        if (escopo.UnidadeUnica is { } uma)
        {
            return query.Where(s => s.UnidadeExecutanteId == uma || s.UnidadeSolicitanteId == uma);
        }

        var unidades = escopo.Unidades;
        return query.Where(s => unidades.Contains(s.UnidadeExecutanteId)
            || (s.UnidadeSolicitanteId != null && unidades.Contains(s.UnidadeSolicitanteId.Value)));
    }

    /// <summary>
    /// Direção da solicitação relativa à unidade de referência: executante ⇒ Recebida, solicitante
    /// ⇒ Enviada. Quando a unidade é as duas coisas, <b>Recebida prevalece</b> (a execução manda).
    /// Sem referência única (visão do conjunto / município) não há seta que faça sentido.
    /// </summary>
    public static SolicitacoesExame.Dtos.DirecaoSolicitacao? Direcao(
        Guid? referencia, Guid executanteId, Guid? solicitanteId) =>
        referencia is not { } r ? null
        : executanteId == r ? SolicitacoesExame.Dtos.DirecaoSolicitacao.Recebida
        : solicitanteId == r ? SolicitacoesExame.Dtos.DirecaoSolicitacao.Enviada
        : null;
}
