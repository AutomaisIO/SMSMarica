using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Unidades;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.PainelInicio.Dtos;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMarica.Core.PainelInicio;

/// <summary>
/// O read model da tela de início: o que precisa da atenção do operador agora, numa requisição só.
/// Não escreve nada — toda ação que o painel oferece é um link para a tela que já sabe fazer aquilo.
/// Ver ADR-0033 (forma), ADR-0034 (cancelamento) e ADR-0035 (pendência de importação).
/// </summary>
public interface IPainelInicioService
{
    Task<PainelInicioDto> ObterAsync(LenteEscopoPainel lente, DirecaoPainel direcao, CancellationToken ct = default);
}

public sealed class PainelInicioService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IIdentidadeService identidade,
    IPacienteResolver pacienteResolver,
    ILogger<PainelInicioService> logger) : IPainelInicioService
{
    /// <summary>Quantas linhas cada raia devolve. O resto é "ver todos" na tela de destino.</summary>
    private const int ItensPorRaia = 5;

    // As janelas moram em JanelasPainel para o "ver todos" da listagem usar o MESMO recorte.
    private const int JanelaAguardandoDias = JanelasPainel.AguardandoDias;
    private const int JanelaConfirmadosDias = JanelasPainel.ConfirmadosDias;

    /// <summary>Estados em que a EQUIPE ainda não agiu — o que mantém a linha nas raias.</summary>
    private static readonly StatusSolicitacao[] EmAberto =
        [StatusSolicitacao.Solicitada, StatusSolicitacao.Agendada];

    public async Task<PainelInicioDto> ObterAsync(
        LenteEscopoPainel lente, DirecaoPainel direcao, CancellationToken ct = default)
    {
        var acoes = await ResolverPermissoesAsync(ct);
        var podeVerSolicitacoes = Tem(acoes, ModuloPermissao.SolicitacoesExame);
        var podeVerSisreg = Tem(acoes, ModuloPermissao.Sisreg);

        var podeMunicipio = await PodeVerMunicipioAsync(acoes, ct);
        if (lente == LenteEscopoPainel.Municipio && !podeMunicipio)
        {
            // 403 e não degradação silenciosa para "unidade": degradar em silêncio esconde erro de
            // permissão e faz o operador achar que o município está vazio (ADR-0033).
            // UnauthorizedAccessException é o que o ExceptionHandlingMiddleware já mapeia para 403.
            throw new UnauthorizedAccessException(
                "A visão do município é restrita a quem tem acesso global ou perfil de regulação.");
        }

        // Na lente Município não há escopo nem "minha" unidade — logo não há seta de direção.
        var escopo = lente == LenteEscopoPainel.Municipio
            ? EscopoUnidadeResultado.Tudo
            : await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        var direcaoEfetiva = lente == LenteEscopoPainel.Municipio ? DirecaoPainel.Tudo : direcao;

        var unidadeNome = escopo.Referencia is { } uref
            ? await db.Unidades.AsNoTracking().Where(u => u.Id == uref).Select(u => u.Nome).FirstOrDefaultAsync(ct)
            : null;

        // Uma raia que falha não pode derrubar a home inteira (RNF5): cai para null, com log.
        var cancelados = podeVerSolicitacoes
            ? await SemDerrubarAsync(nameof(CanceladosAsync), () => CanceladosAsync(escopo, direcaoEfetiva, lente, ct))
            : null;
        var aguardando = podeVerSolicitacoes
            ? await SemDerrubarAsync(nameof(AguardandoAsync), () => AguardandoAsync(escopo, direcaoEfetiva, lente, ct))
            : null;
        var confirmados = podeVerSolicitacoes
            ? await SemDerrubarAsync(nameof(ConfirmadosAsync), () => ConfirmadosAsync(escopo, ct))
            : null;
        var pendencias = podeVerSisreg
            ? await SemDerrubarAsync(nameof(PendenciasAsync), () => PendenciasAsync(escopo, lente, ct))
            : null;

        return new PainelInicioDto(
            lente, podeMunicipio, direcaoEfetiva,
            escopo.Referencia, unidadeNome,
            confirmados, cancelados, aguardando, pendencias,
            JanelaAguardandoDias);
    }

    // ===================== RAIAS =====================

    /// <summary>
    /// "O paciente avisou que não vem." O predicado é o contrato do ADR-0034: resposta de
    /// cancelamento <b>e</b> equipe ainda sem agir. Não se filtra por data futura de propósito —
    /// um cancelado cuja data já passou é justamente o caso esquecido que alguém precisa fechar.
    /// </summary>
    private async Task<RaiaPainel<ItemSolicitacaoPainel>> CanceladosAsync(
        EscopoUnidadeResultado escopo, DirecaoPainel direcao, LenteEscopoPainel lente, CancellationToken ct)
    {
        var q = BaseSolicitacoes(escopo, direcao)
            .Where(s => s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada
                && EmAberto.Contains(s.Status));

        var total = await q.CountAsync(ct);
        var itens = await ProjetarAsync(
            q.OrderByDescending(s => s.ConfirmacaoCanceladaEm), escopo, lente, comMotivo: true, ct);
        return new(total, itens);
    }

    /// <summary>"Mandamos e ele não respondeu, e o exame é logo." Palpite, não fato — por isso é
    /// uma raia separada da de cancelados, e não a mesma coisa em outra cor.</summary>
    private async Task<RaiaPainel<ItemSolicitacaoPainel>> AguardandoAsync(
        EscopoUnidadeResultado escopo, DirecaoPainel direcao, LenteEscopoPainel lente, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var limite = agora.AddDays(JanelaAguardandoDias);

        var q = BaseSolicitacoes(escopo, direcao)
            .Where(s => s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente
                && EmAberto.Contains(s.Status)
                && s.DataAgendada != null && s.DataAgendada >= agora && s.DataAgendada <= limite);

        var total = await q.CountAsync(ct);
        var itens = await ProjetarAsync(
            q.OrderBy(s => s.DataAgendada), escopo, lente, comMotivo: false, ct);
        return new(total, itens);
    }

    /// <summary>Número tranquilo, não fila: quanto está confirmado, separado por direção. Contado
    /// sempre sobre o escopo inteiro (o seletor de direção filtra as raias, não este número).</summary>
    private async Task<ConfirmadosPainel> ConfirmadosAsync(EscopoUnidadeResultado escopo, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var limite = agora.AddDays(JanelaConfirmadosDias);

        var q = BaseSolicitacoes(escopo, DirecaoPainel.Tudo)
            .Where(s => s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada
                && s.DataAgendada != null && s.DataAgendada >= agora && s.DataAgendada <= limite);

        // Sem unidade de referência (visão do conjunto / município) não existe "recebido x enviado":
        // tudo é recebido do ponto de vista da rede. Evita inventar uma direção que não existe.
        if (escopo.Referencia is not { } r)
            return new(await q.CountAsync(ct), 0, JanelaConfirmadosDias);

        var recebidos = await q.CountAsync(s => s.UnidadeExecutanteId == r, ct);
        var enviados = await q.CountAsync(s => s.UnidadeExecutanteId != r && s.UnidadeSolicitanteId == r, ct);
        return new(recebidos, enviados, JanelaConfirmadosDias);
    }

    /// <summary>"Não entrou no sistema." Escopo pela unidade EXECUTANTE — é quem pode resolver.
    /// Pendência sem executante resolvida só aparece na lente Município (ADR-0035 §5).</summary>
    private async Task<RaiaPainel<ItemPendenciaImportacaoPainel>> PendenciasAsync(
        EscopoUnidadeResultado escopo, LenteEscopoPainel lente, CancellationToken ct)
    {
        var q = db.SisregImportacaoFalhas.AsNoTracking().Where(f => f.ResolvidoEm == null);
        if (!escopo.VeTudo)
        {
            var unidades = escopo.Unidades;
            q = q.Where(f => f.UnidadeExecutanteId != null && unidades.Contains(f.UnidadeExecutanteId.Value));
        }

        var total = await q.CountAsync(ct);
        var itens = await q
            .OrderByDescending(f => f.CriadoEm)
            .Take(ItensPorRaia)
            .Select(f => new ItemPendenciaImportacaoPainel(
                f.Id, f.CodigoSolicitacao, f.NomePaciente, f.ProcedimentoTexto, f.DataAgendada,
                f.Causa, f.Motivo, f.Tentativas,
                f.Causa == CausaFalhaImportacao.CpfNaoResolvido,
                lente == LenteEscopoPainel.Municipio ? f.NomeExecutante : null))
            .ToListAsync(ct);

        return new(total, itens);
    }

    // ===================== APOIO =====================

    /// <summary>A base comum das raias de solicitação: viva, no escopo, no recorte de direção.</summary>
    private IQueryable<Solicitacao> BaseSolicitacoes(EscopoUnidadeResultado escopo, DirecaoPainel direcao)
    {
        var q = SolicitacaoNoEscopo.Filtrar(
            db.Solicitacoes.AsNoTracking().Where(s => s.ExcluidoEm == null), escopo);

        // O recorte executante × solicitante só existe com uma unidade de referência — sem ela,
        // "como executante" não quer dizer nada.
        if (escopo.Referencia is not { } r || direcao == DirecaoPainel.Tudo) return q;

        return direcao == DirecaoPainel.Executante
            ? q.Where(s => s.UnidadeExecutanteId == r)
            // Recebida prevalece: uma linha em que a unidade é as duas coisas conta como executante.
            : q.Where(s => s.UnidadeSolicitanteId == r && s.UnidadeExecutanteId != r);
    }

    /// <summary>
    /// Projeta as N primeiras linhas. O <c>Id</c> exposto é o PÚBLICO — o do exame de imagem quando
    /// há satélite, senão o da espinha (ADR-0021) — senão o link do painel abriria uma tela vazia.
    /// </summary>
    private async Task<IReadOnlyList<ItemSolicitacaoPainel>> ProjetarAsync(
        IOrderedQueryable<Solicitacao> q, EscopoUnidadeResultado escopo, LenteEscopoPainel lente,
        bool comMotivo, CancellationToken ct)
    {
        var linhas = await q
            .Take(ItensPorRaia)
            .Select(s => new
            {
                s.Id,
                ExameId = (Guid?)(s.ExameImagem != null ? s.ExameImagem.Id : (Guid?)null),
                s.PacienteId,
                Procedimento = s.EspecialidadeTexto ?? s.ProcedimentoTexto,
                s.DataAgendada,
                s.MotivoCancelamentoPaciente,
                s.ConfirmadoCanal,
                s.ConfirmacaoCanceladaEm,
                s.ConfirmadoEm,
                s.UnidadeExecutanteId,
                s.UnidadeSolicitanteId,
                UnidadeNome = s.UnidadeExecutante != null ? s.UnidadeExecutante.Nome : null,
            })
            .ToListAsync(ct);

        if (linhas.Count == 0) return [];

        // O nome do paciente vive no hub FHIR. Hub fora do ar ⇒ nome null, e o painel ainda serve
        // (data, procedimento e link continuam corretos) — não vale derrubar a home por causa disso.
        IReadOnlyDictionary<Guid, PacienteResumo> nomes;
        try { nomes = await pacienteResolver.ResolverManyAsync(linhas.Select(l => l.PacienteId), ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Painel: não consegui resolver nomes de paciente no hub FHIR.");
            nomes = new Dictionary<Guid, PacienteResumo>();
        }

        return [.. linhas.Select(l => new ItemSolicitacaoPainel(
            l.ExameId ?? l.Id,
            l.PacienteId,
            nomes.TryGetValue(l.PacienteId, out var p) ? p.Nome : null,
            l.Procedimento,
            l.DataAgendada,
            comMotivo ? l.MotivoCancelamentoPaciente : null,
            l.ConfirmadoCanal,
            comMotivo ? l.ConfirmacaoCanceladaEm : l.ConfirmadoEm,
            SolicitacaoNoEscopo.Direcao(escopo.Referencia, l.UnidadeExecutanteId, l.UnidadeSolicitanteId),
            lente == LenteEscopoPainel.Municipio ? l.UnidadeNome : null))];
    }

    private async Task<IReadOnlyDictionary<ModuloPermissao, AcoesPermissao>> ResolverPermissoesAsync(CancellationToken ct)
    {
        if (usuarioAtual.UsuarioId is not { } id) return new Dictionary<ModuloPermissao, AcoesPermissao>();
        var resolvidas = await identidade.ObterPermissoesResolvidasAsync(id, ct);
        return resolvidas.Resolvidas.ToDictionary(p => p.Modulo, p => p.Acoes);
    }

    private static bool Tem(IReadOnlyDictionary<ModuloPermissao, AcoesPermissao> acoes, ModuloPermissao modulo) =>
        acoes.TryGetValue(modulo, out var a) && a.HasFlag(AcoesPermissao.Consulta);

    /// <summary>
    /// Quem enxerga o município: acesso global, ou um dos três módulos de regulação cuja
    /// documentação no enum já diz "visão global do município". Não há módulo novo para o painel
    /// (ADR-0033 §6) — a lente reusa o direito que a pessoa já tem.
    /// </summary>
    private async Task<bool> PodeVerMunicipioAsync(
        IReadOnlyDictionary<ModuloPermissao, AcoesPermissao> acoes, CancellationToken ct) =>
        Tem(acoes, ModuloPermissao.RegulacaoTriagem)
        || Tem(acoes, ModuloPermissao.RegulacaoMedica)
        || Tem(acoes, ModuloPermissao.RegulacaoAgendamento)
        || await AcessoGlobalUsuario.TemAsync(db, usuarioAtual.UsuarioId, ct);

    /// <summary>RNF5: uma raia que explode vira null e o resto da home responde.</summary>
    private async Task<T?> SemDerrubarAsync<T>(string raia, Func<Task<T>> calcular) where T : class
    {
        try { return await calcular(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Painel de início: a raia {Raia} falhou; segue sem ela.", raia);
            return null;
        }
    }
}
