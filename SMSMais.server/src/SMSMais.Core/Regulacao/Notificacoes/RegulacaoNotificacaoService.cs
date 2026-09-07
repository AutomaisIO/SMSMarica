using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Notificacoes;

/// <param name="Escopo"><c>minha</c> (unidades do usuário) ou <c>todas</c> (o município).</param>
public sealed record RegulacaoNotificacaoFiltro(
    string Escopo = "minha",
    bool SoNaoVistas = true,
    int Pagina = 1,
    int Tamanho = 25);

public sealed record RegulacaoNotificacaoDto(
    Guid EventoId,
    Guid SolicitacaoId,
    long NumeroLocal,
    string? NumeroExterno,
    SistemaRegulacao? Sistema,
    TipoEventoRegulacao Tipo,
    StatusRegulacao? De,
    StatusRegulacao? Para,
    string PacienteNome,
    string Procedimento,
    Guid UnidadeSolicitanteId,
    string UnidadeSolicitante,
    DateTime CriadoEm,
    bool Vista);

public sealed record PaginaNotificacoesRegulacaoDto(
    int Total, IReadOnlyList<RegulacaoNotificacaoDto> Itens);

public sealed record RegulacaoNotificacaoResumoDto(int NaoVistas);

public interface IRegulacaoNotificacaoService
{
    Task<PaginaNotificacoesRegulacaoDto> ListarAsync(
        RegulacaoNotificacaoFiltro filtro, CancellationToken ct);

    Task<RegulacaoNotificacaoResumoDto> ResumoAsync(string escopo, CancellationToken ct);

    Task MarcarVistaAsync(Guid eventoId, CancellationToken ct);

    /// <summary>Marca tudo daquela solicitação — é o que abrir o detalhe faz.</summary>
    Task MarcarVistasDaSolicitacaoAsync(Guid solicitacaoId, CancellationToken ct);
}

/// <summary>
/// O que mudou nas solicitações da minha unidade e eu ainda não vi (plano 05).
///
/// <para><b>Notificação aqui não é uma tabela à parte</b>: é uma leitura dos eventos que já
/// existem, filtrada pelos tipos que pedem atenção. Duplicar o fato numa tabela de avisos criaria
/// duas versões da mesma história, que divergem no dia em que uma escrita falha.</para>
///
/// <para>O "visto" é por usuário: a mesma movimentação interessa a quem abriu o pedido e ao
/// agente, e um não pode apagar o aviso do outro.</para>
/// </summary>
public sealed class RegulacaoNotificacaoService(
    SmsMaisDbContext db,
    IRegulacaoEscopo escopoRegulacao,
    IRegulacaoConfiguracaoService configuracao,
    IUsuarioAtualAccessor usuarioAtual) : IRegulacaoNotificacaoService
{
    /// <summary>
    /// Os eventos que pedem atenção de alguém. O resto da trilha (edição, anexo, assumida) conta
    /// a história do caso, mas não é aviso — misturar tudo faria a lista nascer inútil de tão
    /// cheia.
    /// </summary>
    private static readonly TipoEventoRegulacao[] TiposQueAvisam =
    [
        TipoEventoRegulacao.NumeroExterno,
        TipoEventoRegulacao.SituacaoExterna,
        TipoEventoRegulacao.Devolucao,
        TipoEventoRegulacao.Recusa,
        TipoEventoRegulacao.FalhaEnvio,
        TipoEventoRegulacao.PendenciaAberta,
    ];

    public async Task<PaginaNotificacoesRegulacaoDto> ListarAsync(
        RegulacaoNotificacaoFiltro filtro, CancellationToken ct)
    {
        var consulta = await ConsultaAsync(filtro.Escopo, ct);
        if (consulta is null) return new PaginaNotificacoesRegulacaoDto(0, []);

        var usuarioId = usuarioAtual.UsuarioId;

        var comVisto = consulta.Select(e => new
        {
            Evento = e,
            Vista = usuarioId != null
                && db.RegulacaoEventosVistos.Any(v => v.EventoId == e.Id && v.UsuarioId == usuarioId),
        });

        if (filtro.SoNaoVistas) comVisto = comVisto.Where(x => !x.Vista);

        var total = await comVisto.CountAsync(ct);

        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, 200);

        var itens = await comVisto
            // O mais recente primeiro: aviso é sobre o que acabou de acontecer.
            .OrderByDescending(x => x.Evento.CriadoEm)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(x => new RegulacaoNotificacaoDto(
                x.Evento.Id,
                x.Evento.SolicitacaoId,
                x.Evento.Solicitacao!.NumeroLocal,
                x.Evento.Solicitacao.NumeroExterno,
                x.Evento.Solicitacao.SistemaDestino,
                x.Evento.Tipo,
                x.Evento.StatusAnterior,
                x.Evento.StatusNovo,
                x.Evento.Solicitacao.PacienteNome,
                x.Evento.Solicitacao.Procedimento != null
                    ? x.Evento.Solicitacao.Procedimento.NomeCanonico
                    : "(procedimento removido)",
                x.Evento.Solicitacao.UnidadeSolicitanteId,
                x.Evento.Solicitacao.UnidadeSolicitante != null
                    ? x.Evento.Solicitacao.UnidadeSolicitante.Nome
                    : "(unidade removida)",
                x.Evento.CriadoEm,
                x.Vista))
            .ToListAsync(ct);

        return new PaginaNotificacoesRegulacaoDto(total, itens);
    }

    public async Task<RegulacaoNotificacaoResumoDto> ResumoAsync(string escopo, CancellationToken ct)
    {
        var consulta = await ConsultaAsync(escopo, ct);
        if (consulta is null) return new RegulacaoNotificacaoResumoDto(0);

        var usuarioId = usuarioAtual.UsuarioId;
        if (usuarioId is null) return new RegulacaoNotificacaoResumoDto(0);

        var naoVistas = await consulta
            .CountAsync(e => !db.RegulacaoEventosVistos.Any(v => v.EventoId == e.Id && v.UsuarioId == usuarioId), ct);

        return new RegulacaoNotificacaoResumoDto(naoVistas);
    }

    public async Task MarcarVistaAsync(Guid eventoId, CancellationToken ct)
    {
        var usuarioId = usuarioAtual.UsuarioId
            ?? throw new ValidacaoException("usuario", "Sessão sem usuário — refaça o login.");

        // Só marca o que o usuário poderia ver: sem isso, um id adivinhado marcaria como lido um
        // aviso de outra unidade.
        var visivel = await (await ConsultaAsync("minha", ct) ?? Enumerable.Empty<RegulacaoEvento>().AsQueryable())
            .AnyAsync(e => e.Id == eventoId, ct);
        if (!visivel) return;

        if (await db.RegulacaoEventosVistos.AnyAsync(v => v.EventoId == eventoId && v.UsuarioId == usuarioId, ct))
        {
            return;
        }

        db.RegulacaoEventosVistos.Add(new RegulacaoEventoVisto
        {
            EventoId = eventoId,
            UsuarioId = usuarioId,
            VistoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task MarcarVistasDaSolicitacaoAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var usuarioId = usuarioAtual.UsuarioId
            ?? throw new ValidacaoException("usuario", "Sessão sem usuário — refaça o login.");

        var consulta = await ConsultaAsync("minha", ct);
        if (consulta is null) return;

        var pendentes = await consulta
            .Where(e => e.SolicitacaoId == solicitacaoId
                && !db.RegulacaoEventosVistos.Any(v => v.EventoId == e.Id && v.UsuarioId == usuarioId))
            .Select(e => e.Id)
            .ToListAsync(ct);
        if (pendentes.Count == 0) return;

        var agora = DateTime.UtcNow;
        db.RegulacaoEventosVistos.AddRange(pendentes.Select(id => new RegulacaoEventoVisto
        {
            EventoId = id,
            UsuarioId = usuarioId,
            VistoEm = agora,
        }));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Os eventos que avisam, no escopo pedido. <c>null</c> = o usuário não enxerga unidade
    /// nenhuma (fail-closed).
    /// </summary>
    private async Task<IQueryable<RegulacaoEvento>?> ConsultaAsync(string escopo, CancellationToken ct)
    {
        var querTodas = string.Equals(escopo, "todas", StringComparison.OrdinalIgnoreCase);
        if (querTodas)
        {
            // "Todas" é do agente. A ponta só alcança se a configuração do município abrir —
            // é uma decisão de gestão, não um filtro de tela.
            var ehAgente = await escopoRegulacao.EhAgenteAsync(ct);
            var config = await configuracao.ObterEntidadeAsync(ct);
            if (!ehAgente && !config.PontaPodeVerTodasUnidades)
            {
                throw new ValidacaoException(
                    "escopo", "Você só pode ver as notificações das suas unidades.");
            }
        }

        var eventos = db.RegulacaoEventos.AsNoTracking()
            .Include(e => e.Solicitacao).ThenInclude(s => s!.Procedimento)
            .Include(e => e.Solicitacao).ThenInclude(s => s!.UnidadeSolicitante)
            .Where(e => TiposQueAvisam.Contains(e.Tipo) && e.Solicitacao!.ExcluidoEm == null);

        if (querTodas) return eventos;

        var escopoUnidade = await escopoRegulacao.ResolverAsync(ct);
        if (escopoUnidade.SemAcesso) return null;
        if (escopoUnidade.VeTudo) return eventos;

        var unidades = escopoUnidade.Unidades;
        return eventos.Where(e => unidades.Contains(e.Solicitacao!.UnidadeSolicitanteId));
    }
}
