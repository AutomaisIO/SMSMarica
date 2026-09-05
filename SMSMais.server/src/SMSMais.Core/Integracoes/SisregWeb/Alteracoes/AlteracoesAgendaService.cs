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

    public async Task ComunicarAsync(Guid id, CancellationToken ct = default)
    {
        var alteracao = await ObterNoEscopoAsync(id, ct);

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
