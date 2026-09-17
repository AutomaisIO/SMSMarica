using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Regulacao.Notificacoes;
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
    /// <summary>Contadores por tipo e situação; com <paramref name="tecnicos"/>, só o que é deles.</summary>
    Task<SernitNotificacaoResumoDto> ResumoAsync(
        IReadOnlyList<string>? tecnicos, CancellationToken cancellationToken);

    /// <summary>Todos os técnicos que já incluíram solicitação no SERNIT, com as pendências de cada um.</summary>
    Task<IReadOnlyList<TecnicoNotificacaoDto>> TecnicosAsync(CancellationToken cancellationToken);
    Task<SernitNotificacaoPaginaDto> ListarAsync(SernitNotificacaoFiltroDto filtro, CancellationToken cancellationToken);
    Task MarcarVistaAsync(Guid gatilhoId, CancellationToken cancellationToken);
    Task<int> MarcarVistasDaSolicitacaoAsync(string idSernit, CancellationToken cancellationToken);

    /// <summary>Limpa as notificações de <b>Alta</b> que ninguém marcou em
    /// <see cref="LimpezaAltaNotificacao.Prazo"/> — ver <see cref="LimpezaAltaNotificacao"/>.
    /// Devolve quantas saíram da fila.</summary>
    Task<int> LimparAltasAntigasAsync(CancellationToken cancellationToken);
}

public sealed class SernitNotificacaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : ISernitNotificacaoService
{
    // A tela oferece 100/200/500/1000 por página (15/09/2026). Acima do teto a página vinha
    // cortada sem aviso — o operador escolhia 500 e recebia 200.
    private const int TamanhoMaximo = 1000;

    public async Task<SernitNotificacaoResumoDto> ResumoAsync(
        IReadOnlyList<string>? tecnicos, CancellationToken cancellationToken)
    {
        var linhas = await FiltrarTecnicos(PendentesQuery(), tecnicos)
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

    public async Task<IReadOnlyList<TecnicoNotificacaoDto>> TecnicosAsync(
        CancellationToken cancellationToken)
    {
        var nomes = await db.SernitEventos
            .Where(e => e.TipoEvento == TipoEventoExterno.Solicitar
                        && e.Usuario != null && e.Usuario.Trim() != "")
            .Select(e => e.Usuario!.Trim().ToUpper())
            .Distinct()
            .ToListAsync(cancellationToken);

        var pendentes = await PendentesQuery()
            .GroupBy(x => x.Tecnico)
            .Select(g => new { Tecnico = g.Key, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        return TecnicoInclusao.ComPendentes(nomes, pendentes.Select(p => (p.Tecnico, p.Quantidade)));
    }

    public async Task<SernitNotificacaoPaginaDto> ListarAsync(
        SernitNotificacaoFiltroDto filtro, CancellationToken cancellationToken)
    {
        var consulta = FiltrarTecnicos(PendentesQuery(), filtro.Tecnicos);

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
        if (filtro.TipoUltimoEvento is { } tue)
        {
            // O último evento de QUALQUER verbo: "Chegada no Destino" é o fecho do ciclo, e
            // "Transferir"/"Devolvido" são o que a unidade precisa reagir.
            consulta = consulta.Where(x => db.SernitEventos
                .Where(e => e.SernitSolicitacaoId == x.Solicitacao.Id)
                .OrderByDescending(e => e.DataEvento)
                .Select(e => (TipoEventoExterno?)e.TipoEvento)
                .FirstOrDefault() == tue);
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
                    .FirstOrDefault(),
                db.SernitEventos
                    .Where(e => e.SernitSolicitacaoId == x.Solicitacao.Id)
                    .OrderByDescending(e => e.DataEvento)
                    .Select(e => new SernitEventoResumoDto(e.TipoEvento, e.Evento, e.DataEvento))
                    .FirstOrDefault(),
                x.Tecnico))
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

    public Task<int> LimparAltasAntigasAsync(CancellationToken cancellationToken)
    {
        var corte = DateTime.UtcNow - LimpezaAltaNotificacao.Prazo;
        // Update em massa: não passa pelo change tracker, e nem precisa — o worker usa escopo
        // próprio e não lê estas linhas depois.
        return db.SernitGatilhos
            .Where(g => g.ProcessadoEm == null
                        && g.SituacaoAtual == SituacaoSernit.Alta
                        && g.CriadoEm < corte)
            .ExecuteUpdateAsync(u => u
                .SetProperty(g => g.ProcessadoEm, DateTime.UtcNow)
                .SetProperty(g => g.ProcessadoPor, LimpezaAltaNotificacao.ProcessadoPor),
                cancellationToken);
    }

    /// <summary>Gatilho pendente + a solicitação + o técnico que a incluiu (subconsulta
    /// correlacionada, ver <see cref="TecnicoInclusao"/>; só vira SQL onde é usada).</summary>
    private IQueryable<GatilhoComSolicitacao> PendentesQuery() =>
        from g in db.SernitGatilhos
        join s in db.SernitSolicitacoes on g.SernitSolicitacaoId equals s.Id
        where g.ProcessadoEm == null && s.ExcluidoEm == null
        select new GatilhoComSolicitacao
        {
            Gatilho = g,
            Solicitacao = s,
            Tecnico = db.SernitEventos
                .Where(e => e.SernitSolicitacaoId == s.Id
                            && e.TipoEvento == TipoEventoExterno.Solicitar
                            && e.Usuario != null && e.Usuario.Trim() != "")
                .OrderBy(e => e.DataEvento)
                .Select(e => e.Usuario!.Trim().ToUpper())
                .FirstOrDefault(),
        };

    private static IQueryable<GatilhoComSolicitacao> FiltrarTecnicos(
        IQueryable<GatilhoComSolicitacao> consulta, IEnumerable<string>? tecnicos)
    {
        var chaves = TecnicoInclusao.Normalizar(tecnicos);
        if (chaves.Count == 0) return consulta;

        var incluiSemTecnico = chaves.Contains(TecnicoInclusao.SemTecnico);
        var nomes = chaves.Where(c => c != TecnicoInclusao.SemTecnico).ToList();
        return consulta.Where(x =>
            (x.Tecnico == null && incluiSemTecnico) || (x.Tecnico != null && nomes.Contains(x.Tecnico)));
    }

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
        public string? Tecnico { get; init; }
    }
}
