using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Sernit.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit;

/// <summary>
/// <b>Notificações da regulação (SERNIT)</b> — o que mudou desde a última vez que alguém olhou.
/// Primeiro consumidor da fila <c>sernit_gatilho</c>: marcar como visto carimba
/// <c>processado_em</c>, que é o que tira o gatilho da fila.
/// </summary>
public interface ISernitNotificacaoService
{
    Task<SernitNotificacaoResumoDto> ResumoAsync(CancellationToken cancellationToken);
    Task<SernitNotificacaoPaginaDto> ListarAsync(SernitNotificacaoFiltroDto filtro, CancellationToken cancellationToken);
    Task MarcarVistaAsync(Guid gatilhoId, CancellationToken cancellationToken);
    Task<int> MarcarVistasDaSolicitacaoAsync(string idSernit, CancellationToken cancellationToken);
}

public sealed class SernitNotificacaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : ISernitNotificacaoService
{
    // A tela oferece 100/200/500/1000 por página (15/09/2026). Acima do teto a página vinha
    // cortada sem aviso — o operador escolhia 500 e recebia 200.
    private const int TamanhoMaximo = 1000;

    public async Task<SernitNotificacaoResumoDto> ResumoAsync(CancellationToken cancellationToken)
    {
        var linhas = await PendentesQuery()
            .GroupBy(x => new { x.Solicitacao.Tipo, x.Gatilho.SituacaoAtual })
            .Select(g => new { g.Key.Tipo, Situacao = g.Key.SituacaoAtual, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        return new SernitNotificacaoResumoDto(
            linhas.Sum(l => l.Quantidade),
            [.. linhas
                .Where(l => l.Situacao is not null)
                .Select(l => new SernitNotificacaoContadorDto(l.Tipo, l.Situacao!.Value, l.Quantidade))
                .OrderBy(l => l.Tipo).ThenBy(l => l.Situacao)]);
    }

    public async Task<SernitNotificacaoPaginaDto> ListarAsync(
        SernitNotificacaoFiltroDto filtro, CancellationToken cancellationToken)
    {
        var consulta = PendentesQuery();

        if (filtro.Tipo is { } tipo) consulta = consulta.Where(x => x.Solicitacao.Tipo == tipo);
        if (filtro.Situacao is { } sit) consulta = consulta.Where(x => x.Gatilho.SituacaoAtual == sit);
        if (filtro.TipoGatilho is { } tg) consulta = consulta.Where(x => x.Gatilho.Tipo == tg);
        if (!string.IsNullOrWhiteSpace(filtro.CategoriaFollowUp))
        {
            // O filtro olha o ÚLTIMO FollowUP, o mesmo que o card mostra: "falha de contato" é
            // o estado atual da cobrança, não qualquer falha que já tenha existido na trilha.
            var cat = filtro.CategoriaFollowUp.Trim();
            consulta = consulta.Where(x => db.SernitEventos
                .Where(e => e.SernitSolicitacaoId == x.Solicitacao.Id && e.TipoEvento == TipoEventoExterno.FollowUp)
                .OrderByDescending(e => e.DataEvento)
                .Select(e => e.FollowUpCategoria)
                .FirstOrDefault() == cat);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);
        var pagina = Math.Max(1, filtro.Pagina);

        var itens = await consulta
            .OrderByDescending(x => x.Gatilho.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(x => new SernitNotificacaoDto(
                x.Gatilho.Id,
                x.Solicitacao.Id,
                x.Gatilho.IdSernit,
                x.Gatilho.Tipo,
                x.Gatilho.SituacaoAnterior,
                x.Gatilho.SituacaoAtual,
                x.Gatilho.CriadoEm,
                x.Solicitacao.Tipo,
                x.Solicitacao.PacienteNome,
                x.Solicitacao.PacienteId,
                x.Solicitacao.Recurso,
                x.Solicitacao.DataSolicitacao,
                x.Solicitacao.AgendadoParaTexto,
                x.Solicitacao.UnidadeExecutora,
                // Subconsulta correlacionada, uma por linha da página, sobre o verbo TIPADO.
                db.SernitEventos
                    .Where(e => e.SernitSolicitacaoId == x.Solicitacao.Id
                                && e.TipoEvento == TipoEventoExterno.FollowUp)
                    .OrderByDescending(e => e.DataEvento)
                    .Select(e => new SernitFollowUpResumoDto(
                        e.DataEvento, e.Usuario, e.Observacao, e.FollowUpCategoria))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new SernitNotificacaoPaginaDto(itens, total, pagina, tamanho);
    }

    public async Task MarcarVistaAsync(Guid gatilhoId, CancellationToken cancellationToken)
    {
        var gatilho = await db.SernitGatilhos
            .FirstOrDefaultAsync(x => x.Id == gatilhoId, cancellationToken)
            ?? throw new NaoEncontradoException("Notificação do SERNIT", gatilhoId);

        if (gatilho.ProcessadoEm is not null) return;

        Carimbar(gatilho, await QuemAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarcarVistasDaSolicitacaoAsync(string idSernit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idSernit))
        {
            throw new ValidacaoException("sernit.id_obrigatorio", "Informe a solicitação.");
        }

        var pendentes = await db.SernitGatilhos
            .Where(x => x.IdSernit == idSernit && x.ProcessadoEm == null)
            .ToListAsync(cancellationToken);

        var quem = await QuemAsync(cancellationToken);
        foreach (var g in pendentes) Carimbar(g, quem);
        await db.SaveChangesAsync(cancellationToken);
        return pendentes.Count;
    }

    private IQueryable<GatilhoComSolicitacao> PendentesQuery() =>
        from g in db.SernitGatilhos
        join s in db.SernitSolicitacoes on g.SernitSolicitacaoId equals s.Id
        where g.ProcessadoEm == null && s.ExcluidoEm == null
        select new GatilhoComSolicitacao { Gatilho = g, Solicitacao = s };

    private static void Carimbar(SernitGatilho gatilho, string? quem)
    {
        gatilho.ProcessadoEm = DateTime.UtcNow;
        gatilho.ProcessadoPor = quem;
    }

    private async Task<string?> QuemAsync(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } id) return null;
        var nome = await db.Usuarios
            .Where(u => u.Id == id)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(nome) ? id.ToString() : nome;
    }

    private sealed class GatilhoComSolicitacao
    {
        public required SernitGatilho Gatilho { get; init; }
        public required SernitSolicitacao Solicitacao { get; init; }
    }
}
