using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Regulacao.Notificacoes;
using SMSMais.Data;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.EsusSg;

public sealed record EsusSgNotificacaoDto(
    Guid Id,
    Guid SolicitacaoId,
    string IdEsusSg,
    TipoGatilhoEsusSg Tipo,
    SituacaoEsusSg? SituacaoAnterior,
    SituacaoEsusSg? SituacaoAtual,
    DateTime CriadoEm,
    TipoRecursoEsusSg TipoRecurso,
    string PacienteNome,
    Guid? PacienteId,
    string Recurso,
    string? Prioridade,
    DateOnly? DataEntradaFila,
    DateOnly? DataAgendada,
    string? DataHoraAgendadaTexto,
    string? UnidadeExecutora,
    string? PayloadJson,
    EsusSgEventoResumoDto? UltimoEvento,
    string? Tecnico);

public sealed record EsusSgEventoResumoDto(TipoEventoExterno Tipo, string Evento, DateTime DataEvento);

public sealed record EsusSgNotificacaoPaginaDto(
    IReadOnlyList<EsusSgNotificacaoDto> Itens, int Total, int Pagina, int Tamanho);

public sealed record EsusSgNotificacaoContadorDto(TipoRecursoEsusSg Tipo, SituacaoEsusSg Situacao, int Quantidade);

public sealed record EsusSgNotificacaoResumoDto(int Total, IReadOnlyList<EsusSgNotificacaoContadorDto> Contadores);

public sealed record EsusSgNotificacaoFiltroDto
{
    public TipoRecursoEsusSg? Tipo { get; init; }
    public SituacaoEsusSg? Situacao { get; init; }
    public TipoGatilhoEsusSg? TipoGatilho { get; init; }
    public List<string>? Tecnicos { get; init; }
    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 50;
}

/// <summary>
/// Consumidor da fila <c>esussg_gatilho</c> — a tela de Notificações do ESUS SG (irmã da do SERNIT).
/// "Técnico" é o servidor de Maricá que incluiu o pedido na fila do ESUS (<c>usuario_inclusao</c>),
/// o mesmo papel que o autor do "Solicitar" tem no SER/SERNIT.
/// </summary>
public interface IEsusSgNotificacaoService
{
    Task<EsusSgNotificacaoResumoDto> ResumoAsync(IReadOnlyList<string>? tecnicos, CancellationToken cancellationToken);
    Task<IReadOnlyList<TecnicoNotificacaoDto>> TecnicosAsync(CancellationToken cancellationToken);
    Task<EsusSgNotificacaoPaginaDto> ListarAsync(EsusSgNotificacaoFiltroDto filtro, CancellationToken cancellationToken);
    Task MarcarVistaAsync(Guid gatilhoId, CancellationToken cancellationToken);
    Task<int> MarcarVistasDaSolicitacaoAsync(string idEsusSg, CancellationToken cancellationToken);

    /// <summary>Tira da fila as saídas da fila antigas (mesmo prazo da limpeza de altas do SER/SERNIT).</summary>
    Task<int> LimparSaidasAntigasAsync(CancellationToken cancellationToken);
}

public sealed class EsusSgNotificacaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IEsusSgNotificacaoService
{
    private const int TamanhoMaximo = 1000;

    public async Task<EsusSgNotificacaoResumoDto> ResumoAsync(
        IReadOnlyList<string>? tecnicos, CancellationToken cancellationToken)
    {
        var linhas = await FiltrarTecnicos(PendentesQuery(), tecnicos)
            .GroupBy(x => new { x.Solicitacao.Tipo, x.Gatilho.SituacaoAtual })
            .Select(g => new { g.Key.Tipo, Situacao = g.Key.SituacaoAtual, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        return new EsusSgNotificacaoResumoDto(
            linhas.Sum(l => l.Quantidade),
            [.. linhas.Where(l => l.Situacao is not null)
                .Select(l => new EsusSgNotificacaoContadorDto(l.Tipo, l.Situacao!.Value, l.Quantidade))
                .OrderBy(l => l.Tipo).ThenBy(l => l.Situacao)]);
    }

    public async Task<IReadOnlyList<TecnicoNotificacaoDto>> TecnicosAsync(CancellationToken cancellationToken)
    {
        var nomes = await db.EsusSgSolicitacoes
            .Where(s => s.ExcluidoEm == null && s.UsuarioInclusao != null && s.UsuarioInclusao.Trim() != "")
            .Select(s => s.UsuarioInclusao!.Trim().ToUpper())
            .Distinct()
            .ToListAsync(cancellationToken);

        var pendentes = await PendentesQuery()
            .GroupBy(x => x.Tecnico)
            .Select(g => new { Tecnico = g.Key, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        return TecnicoInclusao.ComPendentes(nomes, pendentes.Select(p => (p.Tecnico, p.Quantidade)));
    }

    public async Task<EsusSgNotificacaoPaginaDto> ListarAsync(
        EsusSgNotificacaoFiltroDto filtro, CancellationToken cancellationToken)
    {
        var consulta = FiltrarTecnicos(PendentesQuery(), filtro.Tecnicos);
        if (filtro.Tipo is { } tipo) consulta = consulta.Where(x => x.Solicitacao.Tipo == tipo);
        if (filtro.Situacao is { } sit) consulta = consulta.Where(x => x.Gatilho.SituacaoAtual == sit);
        if (filtro.TipoGatilho is { } tg) consulta = consulta.Where(x => x.Gatilho.Tipo == tg);

        var total = await consulta.CountAsync(cancellationToken);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);
        var pagina = Math.Max(1, filtro.Pagina);

        var itens = await consulta
            .OrderByDescending(x => x.Gatilho.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(x => new EsusSgNotificacaoDto(
                x.Gatilho.Id,
                x.Solicitacao.Id,
                x.Gatilho.IdEsusSg,
                x.Gatilho.Tipo,
                x.Gatilho.SituacaoAnterior,
                x.Gatilho.SituacaoAtual,
                x.Gatilho.CriadoEm,
                x.Solicitacao.Tipo,
                x.Solicitacao.PacienteNome,
                x.Solicitacao.PacienteId,
                x.Solicitacao.Recurso,
                x.Solicitacao.Prioridade,
                x.Solicitacao.DataEntradaFila,
                x.Solicitacao.DataAgendada,
                x.Solicitacao.DataHoraAgendadaTexto,
                x.Solicitacao.UnidadeExecutora,
                x.Gatilho.PayloadJson,
                db.EsusSgEventos
                    .Where(e => e.EsusSgSolicitacaoId == x.Solicitacao.Id)
                    .OrderByDescending(e => e.DataEvento)
                    .Select(e => new EsusSgEventoResumoDto(e.TipoEvento, e.Evento, e.DataEvento))
                    .FirstOrDefault(),
                x.Tecnico))
            .ToListAsync(cancellationToken);

        return new EsusSgNotificacaoPaginaDto(itens, total, pagina, tamanho);
    }

    public async Task MarcarVistaAsync(Guid gatilhoId, CancellationToken cancellationToken)
    {
        var gatilho = await db.EsusSgGatilhos.FirstOrDefaultAsync(x => x.Id == gatilhoId, cancellationToken)
            ?? throw new NaoEncontradoException("Notificação do ESUS SG", gatilhoId);
        if (gatilho.ProcessadoEm is not null) return;
        gatilho.ProcessadoEm = DateTime.UtcNow;
        gatilho.ProcessadoPor = await QuemAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarcarVistasDaSolicitacaoAsync(string idEsusSg, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idEsusSg))
        {
            throw new ValidacaoException("esussg.id_obrigatorio", "Informe o pedido.");
        }

        var pendentes = await db.EsusSgGatilhos
            .Where(x => x.IdEsusSg == idEsusSg && x.ProcessadoEm == null)
            .ToListAsync(cancellationToken);
        var quem = await QuemAsync(cancellationToken);
        foreach (var g in pendentes)
        {
            g.ProcessadoEm = DateTime.UtcNow;
            g.ProcessadoPor = quem;
        }
        await db.SaveChangesAsync(cancellationToken);
        return pendentes.Count;
    }

    public Task<int> LimparSaidasAntigasAsync(CancellationToken cancellationToken)
    {
        var corte = DateTime.UtcNow - LimpezaAltaNotificacao.Prazo;
        return db.EsusSgGatilhos
            .Where(g => g.ProcessadoEm == null && g.SituacaoAtual == SituacaoEsusSg.SaiuDaFila && g.CriadoEm < corte)
            .ExecuteUpdateAsync(u => u
                .SetProperty(g => g.ProcessadoEm, DateTime.UtcNow)
                .SetProperty(g => g.ProcessadoPor, LimpezaAltaNotificacao.ProcessadoPor),
                cancellationToken);
    }

    private IQueryable<GatilhoComSolicitacao> PendentesQuery() =>
        from g in db.EsusSgGatilhos
        join s in db.EsusSgSolicitacoes on g.EsusSgSolicitacaoId equals s.Id
        where g.ProcessadoEm == null && s.ExcluidoEm == null
        select new GatilhoComSolicitacao
        {
            Gatilho = g,
            Solicitacao = s,
            Tecnico = s.UsuarioInclusao == null || s.UsuarioInclusao.Trim() == ""
                ? null
                : s.UsuarioInclusao.Trim().ToUpper(),
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

    private async Task<string?> QuemAsync(CancellationToken cancellationToken)
    {
        if (usuarioAtual.UsuarioId is not { } id) return null;
        var nome = await db.Usuarios.Where(u => u.Id == id).Select(u => u.NomeCompleto).FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(nome) ? id.ToString() : nome;
    }

    private sealed class GatilhoComSolicitacao
    {
        public required EsusSgGatilho Gatilho { get; init; }
        public required EsusSgSolicitacao Solicitacao { get; init; }
        public string? Tecnico { get; init; }
    }
}
