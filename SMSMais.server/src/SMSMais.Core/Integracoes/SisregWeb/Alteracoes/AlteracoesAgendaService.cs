using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Alteracoes;

/// <summary>Uma alteração na fila, com o contexto que o regulador precisa para decidir.</summary>
public sealed record AlteracaoAgendaDto(
    Guid Id,
    Guid SolicitacaoId,
    string? CodigoSolicitacao,
    TipoAlteracaoAgenda Tipo,
    string? ValorAntes,
    string? ValorDepois,
    string? PacienteNome,
    string? ProcedimentoTexto,
    string? UnidadeExecutanteNome,
    string? UnidadeSolicitanteNome,
    DateTime? DataAgendada,
    DateTime DetectadaEm,
    DateTime? TratadaEm,
    DateTime? ComunicadaEm);

public sealed record PaginaAlteracoesAgendaDto(int Total, IReadOnlyList<AlteracaoAgendaDto> Itens);

public interface IAlteracoesAgendaService
{
    Task<PaginaAlteracoesAgendaDto> ListarAsync(
        bool apenasPendentes, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>Marca como resolvida — o regulador viu e decidiu que não há mais o que fazer.</summary>
    Task TratarAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Trata várias de uma vez. Devolve quantas foram efetivamente marcadas.
    ///
    /// <para><b>Por que o lote existe:</b> a fila é alimentada por máquina e esvaziada por pessoa.
    /// Em 06/09/2026 ela amanheceu com 1.378 linhas, das quais ~34 eram reais — uma por clique,
    /// eram semanas de trabalho para chegar às que importavam. Corrigidas as causas, a fila voltou
    /// a ser pequena; o botão fica porque o desequilíbrio entre quem enche e quem esvazia não.</para>
    /// </summary>
    Task<int> TratarLoteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Reenvia a confirmação ao paciente com os dados ATUAIS e marca a alteração como comunicada.
    /// Revoga os links anteriores: quem tem na mão a data velha perde o acesso a ela.
    /// </summary>
    Task ComunicarAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// A fila de alterações que o SISREG fez em agendamentos já importados.
///
/// <para><b>Para que serve:</b> quando o SISREG remarca uma consulta, o paciente fica com um dia na
/// mão que não vale mais e ninguém deste lado sabe. Esta fila existe para que o agente de regulação
/// e a unidade solicitante vejam o que mudou e tomem providência — inclusive avisar o paciente.</para>
///
/// <para><b>Escopo por unidade</b> (ADR-0033): quem opera uma unidade vê o que é dela, seja como
/// executante ou como solicitante. A unidade solicitante entra de propósito — é ela que conhece o
/// paciente e costuma ser quem consegue falar com ele.</para>
/// </summary>
public sealed class AlteracoesAgendaService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IComunicacaoPacienteService comunicacao) : IAlteracoesAgendaService
{
    public async Task<PaginaAlteracoesAgendaDto> ListarAsync(
        bool apenasPendentes, int pagina, int tamanho, CancellationToken ct = default)
    {
        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);

        var query = db.SisregAlteracoesAgenda.AsNoTracking().AsQueryable();

        if (apenasPendentes) query = query.Where(a => a.TratadaEm == null);

        if (!escopo.VeTudo)
        {
            // Executante OU solicitante: a unidade que pediu o exame é quem conhece o paciente e
            // muitas vezes é a única que consegue falar com ele.
            query = query.Where(a =>
                (a.UnidadeExecutanteId != null && escopo.Unidades.Contains(a.UnidadeExecutanteId.Value))
                || (a.UnidadeSolicitanteId != null && escopo.Unidades.Contains(a.UnidadeSolicitanteId.Value)));
        }

        var total = await query.CountAsync(ct);

        var itens = await query
            .OrderBy(a => a.TratadaEm != null)
            .ThenByDescending(a => a.DetectadaEm)
            .Skip(Math.Max(0, pagina) * Math.Clamp(tamanho, 1, 200))
            .Take(Math.Clamp(tamanho, 1, 200))
            .Select(a => new AlteracaoAgendaDto(
                a.Id,
                a.SolicitacaoId,
                a.CodigoSolicitacao,
                a.Tipo,
                a.ValorAntes,
                a.ValorDepois,
                null,
                a.Solicitacao!.ProcedimentoTexto,
                db.Unidades.Where(u => u.Id == a.UnidadeExecutanteId).Select(u => u.Nome).FirstOrDefault(),
                db.Unidades.Where(u => u.Id == a.UnidadeSolicitanteId).Select(u => u.Nome).FirstOrDefault(),
                a.Solicitacao.DataAgendada,
                a.DetectadaEm,
                a.TratadaEm,
                a.ComunicadaEm))
            .ToListAsync(ct);

        return new PaginaAlteracoesAgendaDto(total, itens);
    }

    public async Task TratarAsync(Guid id, CancellationToken ct = default)
    {
        var alteracao = await ObterNoEscopoAsync(id, ct);

        // Idempotente: dois cliques (ou dois operadores) não podem reescrever a autoria de quem
        // tratou primeiro.
        if (alteracao.TratadaEm is not null) return;

        alteracao.TratadaEm = DateTime.UtcNow;
        alteracao.TratadaPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Teto por chamada. Alto o bastante para uma tela cheia, baixo o bastante para o
    /// lote continuar sendo uma decisão sobre linhas que o operador viu.</summary>
    private const int MaxDoLote = 200;

    public async Task<int> TratarLoteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return 0;

        if (ids.Count > MaxDoLote)
        {
            throw new ValidacaoException(
                "alteracao.lote_grande_demais",
                $"São {ids.Count} alterações de uma vez, e o limite é {MaxDoLote}. Trate por página.");
        }

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);

        // Filtra pelo MESMO escopo da listagem, e não por confiança no que o front mandou: um id
        // de outra unidade colado na requisição não pode ser tratado por quem não o enxerga.
        var query = db.SisregAlteracoesAgenda
            .Where(a => ids.Contains(a.Id) && a.TratadaEm == null);

        if (!escopo.VeTudo)
        {
            query = query.Where(a =>
                (a.UnidadeExecutanteId != null && escopo.Unidades.Contains(a.UnidadeExecutanteId.Value))
                || (a.UnidadeSolicitanteId != null && escopo.Unidades.Contains(a.UnidadeSolicitanteId.Value)));
        }

        var alteracoes = await query.ToListAsync(ct);
        if (alteracoes.Count == 0) return 0;

        var agora = DateTime.UtcNow;
        foreach (var alteracao in alteracoes)
        {
            alteracao.TratadaEm = agora;
            alteracao.TratadaPor = usuarioAtual.UsuarioId;
        }

        await db.SaveChangesAsync(ct);
        return alteracoes.Count;
    }

    public async Task ComunicarAsync(Guid id, CancellationToken ct = default)
    {
        var alteracao = await ObterNoEscopoAsync(id, ct);

        // "Sumiu do SISREG" não tem o que comunicar, e comunicar é PIOR que não fazer nada: a
        // mensagem que sai é uma CONFIRMAÇÃO de agendamento montada com os dados de agora — e os
        // dados de agora são a data velha, porque a ausência não altera a solicitação. O paciente
        // receberia a confirmação de um horário que muito provavelmente foi cancelado lá, com link
        // válido, no mesmo momento em que a unidade ainda nem sabe se a vaga existe.
        //
        // A providência aqui é outra: conferir no SISREG e, se caiu mesmo, cancelar deste lado.
        if (alteracao.Tipo == TipoAlteracaoAgenda.Ausente)
        {
            throw new ValidacaoException(
                "alteracao.ausente_nao_se_comunica",
                "Este agendamento sumiu do arquivo do SISREG — não dá para avisar o paciente a "
                + "partir daqui, porque a mensagem seria uma confirmação do horário antigo, que "
                + "provavelmente não existe mais. Confirme no SISREG: se foi cancelado, cancele a "
                + "solicitação; se continua de pé, use \"Só tratar\".");
        }

        // `EnviarManualAsync` reconstrói a comunicação com os dados de AGORA e revoga os magic links
        // anteriores — é justamente o que se quer numa remarcação: o link com a data velha morre.
        await comunicacao.EnviarManualAsync(
            alteracao.SolicitacaoId, FinalidadeComunicacao.ConfirmacaoAgendamento,
            assumirRisco: false, ct);

        alteracao.ComunicadaEm = DateTime.UtcNow;
        alteracao.ComunicadaPor = usuarioAtual.UsuarioId;

        // Avisar o paciente É a providência: deixar a linha pendente depois disso só faria a fila
        // crescer com o que já foi resolvido.
        alteracao.TratadaEm ??= DateTime.UtcNow;
        alteracao.TratadaPor ??= usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(ct);
    }

    private async Task<Data.Entities.Sisreg.SisregAlteracaoAgenda> ObterNoEscopoAsync(
        Guid id, CancellationToken ct)
    {
        var alteracao = await db.SisregAlteracoesAgenda.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NaoEncontradoException("Alteração de agenda", id);

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        if (escopo.VeTudo) return alteracao;

        var minha =
            (alteracao.UnidadeExecutanteId is { } exec && escopo.Unidades.Contains(exec))
            || (alteracao.UnidadeSolicitanteId is { } solic && escopo.Unidades.Contains(solic));

        if (!minha)
        {
            // NaoEncontrado, não Proibido: a existência da alteração de outra unidade já é
            // informação que este operador não deveria ter.
            throw new NaoEncontradoException("Alteração de agenda", id);
        }

        return alteracao;
    }
}
