using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Ser;

/// <summary>
/// <b>Notificações da regulação</b> — o que mudou no SER desde a última vez que alguém olhou.
///
/// <para>É o <b>primeiro consumidor da fila de gatilhos</b>. Até aqui `ser_gatilho` só enchia:
/// o motor registrava cada movimento (solicitação nova, mudança de situação, remarcação, FollowUP
/// novo) e ninguém lia. Esta tela lê — e ao marcar como visto carimba `processado_em`, que é
/// exatamente o que tira o gatilho da fila.</para>
///
/// <para><b>Por que isso também valida o motor:</b> gatilho que não aparece aqui é movimento que o
/// espelho não viu; gatilho que aparece errado é diff mal feito. Usar a fila é a forma honesta de
/// descobrir se ela funciona, antes de pendurar automação nela.</para>
/// </summary>
public interface ISerNotificacaoService
{
    /// <summary>Contadores do que está por ler, quebrados por tipo de recurso e situação —
    /// alimenta as abas (Consulta/Exame) e os números por situação.</summary>
    Task<SerNotificacaoResumoDto> ResumoAsync(CancellationToken cancellationToken);

    Task<SerNotificacaoPaginaDto> ListarAsync(
        SerNotificacaoFiltroDto filtro, CancellationToken cancellationToken);

    /// <summary>Marca um movimento como visto. Some da tela e sai da fila de gatilhos.</summary>
    Task MarcarVistaAsync(Guid gatilhoId, CancellationToken cancellationToken);

    /// <summary>Marca de uma vez tudo que está pendente de UMA solicitação — é o botão da linha:
    /// quem viu a movimentação viu a solicitação inteira.</summary>
    Task<int> MarcarVistasDaSolicitacaoAsync(string idSer, CancellationToken cancellationToken);
}

public sealed class SerNotificacaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : ISerNotificacaoService
{
    // A tela oferece 100/200/500/1000 por página (15/09/2026). Acima do teto a página vinha
    // cortada sem aviso — o operador escolhia 500 e recebia 200.
    private const int TamanhoMaximo = 1000;

    public async Task<SerNotificacaoResumoDto> ResumoAsync(CancellationToken cancellationToken)
    {
        // Agrupa no banco: a fila passa de 30 mil e trazer tudo para contar em memória seria
        // desperdício num endpoint que a tela chama a cada poucos segundos.
        var linhas = await PendentesQuery()
            .GroupBy(x => new { x.Solicitacao.Tipo, x.Gatilho.SituacaoAtual })
            .Select(g => new
            {
                g.Key.Tipo,
                Situacao = g.Key.SituacaoAtual,
                Quantidade = g.Count(),
            })
            .ToListAsync(cancellationToken);

        return new SerNotificacaoResumoDto(
            linhas.Sum(l => l.Quantidade),
            [.. linhas
                .Where(l => l.Situacao is not null)
                .Select(l => new SerNotificacaoContadorDto(l.Tipo, l.Situacao!.Value, l.Quantidade))
                .OrderBy(l => l.Tipo).ThenBy(l => l.Situacao)]);
    }

    public async Task<SerNotificacaoPaginaDto> ListarAsync(
        SerNotificacaoFiltroDto filtro, CancellationToken cancellationToken)
    {
        var consulta = PendentesQuery();

        if (filtro.Tipo is { } tipo) consulta = consulta.Where(x => x.Solicitacao.Tipo == tipo);
        if (filtro.Situacao is { } sit) consulta = consulta.Where(x => x.Gatilho.SituacaoAtual == sit);
        if (filtro.TipoGatilho is { } tg) consulta = consulta.Where(x => x.Gatilho.Tipo == tg);

        var total = await consulta.CountAsync(cancellationToken);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);
        var pagina = Math.Max(1, filtro.Pagina);

        var itens = await consulta
            // Mais recente primeiro: notificação é sobre o que acabou de acontecer.
            .OrderByDescending(x => x.Gatilho.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(x => new SerNotificacaoDto(
                x.Gatilho.Id,
                x.Solicitacao.Id,
                x.Gatilho.IdSer,
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
                // Subconsulta correlacionada (uma por linha da página, resolvida no banco). O
                // padrão "%follow%up%" cobre as grafias que o sincronizador tolera ("FollowUP",
                // "Follow-UP", "follow up") — ver EhFollowUp em SerSincronizacaoService.
                db.SerEventos
                    .Where(e => e.SerSolicitacaoId == x.Solicitacao.Id
                                && EF.Functions.ILike(e.Evento, "%follow%up%"))
                    .OrderByDescending(e => e.DataEvento)
                    .Select(e => new SerFollowUpResumoDto(e.DataEvento, e.Usuario, e.Observacao))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new SerNotificacaoPaginaDto(itens, total, pagina, tamanho);
    }

    public async Task MarcarVistaAsync(Guid gatilhoId, CancellationToken cancellationToken)
    {
        var gatilho = await db.SerGatilhos
            .FirstOrDefaultAsync(x => x.Id == gatilhoId, cancellationToken)
            ?? throw new NaoEncontradoException("Notificação do SER", gatilhoId);

        // Já visto por outra pessoa não é erro: duas pessoas olhando a mesma fila é o normal.
        if (gatilho.ProcessadoEm is not null) return;

        Carimbar(gatilho, await QuemAsync(cancellationToken));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarcarVistasDaSolicitacaoAsync(
        string idSer, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idSer))
        {
            throw new ValidacaoException("ser.id_obrigatorio", "Informe a solicitação.");
        }

        var pendentes = await db.SerGatilhos
            .Where(x => x.IdSer == idSer && x.ProcessadoEm == null)
            .ToListAsync(cancellationToken);

        var quem = await QuemAsync(cancellationToken);
        foreach (var g in pendentes) Carimbar(g, quem);
        await db.SaveChangesAsync(cancellationToken);
        return pendentes.Count;
    }

    // ------------------------------------------------------------------ interno

    /// <summary>
    /// Gatilho pendente + a solicitação dele. O join é explícito porque a tela mostra paciente e
    /// recurso, que moram na solicitação, e agrupa por tipo de recurso, que é dela também — o
    /// gatilho sozinho não sabe se é consulta ou exame.
    /// </summary>
    private IQueryable<GatilhoComSolicitacao> PendentesQuery() =>
        from g in db.SerGatilhos
        join s in db.SerSolicitacoes on g.SerSolicitacaoId equals s.Id
        where g.ProcessadoEm == null && s.ExcluidoEm == null
        select new GatilhoComSolicitacao { Gatilho = g, Solicitacao = s };

    private static void Carimbar(SerGatilho gatilho, string? quem)
    {
        gatilho.ProcessadoEm = DateTime.UtcNow;
        // Quem viu importa tanto quanto o fato de ter sido visto: é o que permite, depois,
        // perguntar a alguém sobre uma movimentação específica.
        gatilho.ProcessadoPor = quem;
    }

    /// <summary>Nome de quem está marcando. O accessor só carrega o id, então o nome vem do banco
    /// — uma consulta por ação, e a ação é humana (clique), não de laço.</summary>
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
        public required SerGatilho Gatilho { get; init; }
        public required SerSolicitacao Solicitacao { get; init; }
    }
}
