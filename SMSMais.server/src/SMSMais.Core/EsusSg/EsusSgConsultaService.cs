using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.EsusSg.Dtos;
using SMSMais.Core.Regulacao.AnaliseRegras;
using SMSMais.Core.Regulacao.AnaliseRegras.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;

namespace SMSMais.Core.EsusSg;

/// <summary>Leitura do espelho do ESUS SG para as telas (fila, resumo, detalhe). Só leitura.</summary>
public interface IEsusSgConsultaService
{
    Task<EsusSgBuscaResultadoDto> BuscarAsync(EsusSgBuscaFiltroDto filtro, CancellationToken cancellationToken);
    Task<EsusSgResumoDto> ResumoAsync(CancellationToken cancellationToken);
    Task<EsusSgSolicitacaoDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class EsusSgConsultaService(
    SmsMaisDbContext db,
    IAnaliseRegrasEspelhoService analise) : IEsusSgConsultaService
{
    public async Task<EsusSgBuscaResultadoDto> BuscarAsync(
        EsusSgBuscaFiltroDto filtro, CancellationToken cancellationToken)
    {
        var tamanho = Math.Clamp(filtro.Tamanho, 1, 200);
        var pagina = Math.Max(1, filtro.Pagina);

        var q = db.EsusSgSolicitacoes.AsNoTracking().Where(x => x.ExcluidoEm == null);

        if (filtro.Situacao is { } sit) q = q.Where(x => x.Situacao == sit);
        if (filtro.Tipo is { } tipo) q = q.Where(x => x.Tipo == tipo);
        if (!string.IsNullOrWhiteSpace(filtro.Recurso)) q = q.Where(x => x.Recurso == filtro.Recurso);
        if (!string.IsNullOrWhiteSpace(filtro.Prioridade)) q = q.Where(x => x.Prioridade == filtro.Prioridade);
        if (filtro.EntradaInicio is { } ei) q = q.Where(x => x.DataEntradaFila >= ei);
        if (filtro.EntradaFim is { } ef) q = q.Where(x => x.DataEntradaFila <= ef);
        if (filtro.AgendadaInicio is { } ai) q = q.Where(x => x.DataAgendada >= ai);
        if (filtro.AgendadaFim is { } af) q = q.Where(x => x.DataAgendada <= af);
        if (filtro.MudouDesde is { } md) q = q.Where(x => x.SituacaoMudouEm >= md);

        if (!string.IsNullOrWhiteSpace(filtro.Termo))
        {
            var termo = filtro.Termo.Trim();
            var digitos = new string(termo.Where(char.IsDigit).ToArray());
            var padrao = $"%{termo}%";
            q = digitos.Length >= 4 && digitos.Length == termo.Length
                ? q.Where(x => x.IdEsusSg == digitos || x.Cpf == digitos || x.Cns == digitos)
                : q.Where(x => EF.Functions.ILike(x.PacienteNome, padrao));
        }

        if (filtro.Veredito is { } veredito)
        {
            var comVeredito = db.RegulacaoAnalisesEspelho
                .Where(a => a.Sistema == SistemaRegulacao.EsusSg && a.Veredito == veredito)
                .Select(a => a.EspelhoId);
            q = q.Where(x => comVeredito.Contains(x.Id));
        }

        var total = await q.CountAsync(cancellationToken);

        // Fila: os mais antigos na espera primeiro (é quem mais precisa de olho). Agendados: o
        // atendimento mais próximo primeiro.
        var ordenada = filtro.Situacao == SituacaoEsusSg.Agendada
            ? q.OrderBy(x => x.DataAgendada).ThenBy(x => x.PacienteNome)
            : q.OrderBy(x => x.DataEntradaFila).ThenBy(x => x.IdEsusSg);

        var linhas = await ordenada.Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(cancellationToken);
        var resumos = await analise.ResumosAsync(SistemaRegulacao.EsusSg, linhas.Select(l => l.Id).ToList(), cancellationToken);
        var hoje = FusoBrasilia.HojeEmBrasilia();

        return new EsusSgBuscaResultadoDto(
            linhas.Select(l => ParaLista(l, resumos.GetValueOrDefault(l.Id), hoje)).ToList(),
            total, pagina, tamanho);
    }

    public async Task<EsusSgResumoDto> ResumoAsync(CancellationToken cancellationToken)
    {
        var ativos = db.EsusSgSolicitacoes.AsNoTracking().Where(x => x.ExcluidoEm == null);

        var porSituacao = await ativos.GroupBy(x => x.Situacao)
            .Select(g => new EsusSgResumoSituacaoDto(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var porRecurso = await ativos
            .Where(x => x.Situacao == SituacaoEsusSg.EmFila || x.Situacao == SituacaoEsusSg.Pendente)
            .GroupBy(x => x.Recurso)
            .Select(g => new EsusSgContagemTextoDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Quantidade)
            .ToListAsync(cancellationToken);

        var hoje = FusoBrasilia.HojeEmBrasilia();
        var ate = hoje.AddDays(30);
        var proximos = await ativos.CountAsync(
            x => x.Situacao == SituacaoEsusSg.Agendada && x.DataAgendada >= hoje && x.DataAgendada <= ate,
            cancellationToken);

        var porVeredito = await analise.ContagemAsync(SistemaRegulacao.EsusSg, cancellationToken);

        return new EsusSgResumoDto(porSituacao, porVeredito, porRecurso, proximos);
    }

    public async Task<EsusSgSolicitacaoDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        var s = await db.EsusSgSolicitacoes.AsNoTracking()
                    .Include(x => x.Eventos)
                    .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, cancellationToken)
                ?? throw new NaoEncontradoException("Pedido do ESUS SG", id);

        var detalheAnalise = await analise.ObterDetalheAsync(SistemaRegulacao.EsusSg, id, cancellationToken);
        var hoje = FusoBrasilia.HojeEmBrasilia();

        return new EsusSgSolicitacaoDetalheDto(
            ParaLista(s, detalheAnalise?.Resumo, hoje),
            s.CodigoInterno, s.Subprocedimentos, s.ProfissionalSolicitante, s.UnidadeSolicitante,
            s.UsuarioInclusao, s.Regulador, s.OrdemEntrada, s.Sexo, s.NomeMae, s.Telefone, s.Celular,
            s.MunicipioPaciente, s.Bairro, s.CnesExecutora, s.Setor, s.Local, s.UsuarioAgendamento,
            s.AgendamentoCadastradoEm, s.DataSaidaFila, s.ComprovanteImpresso, s.AgendadoTfd,
            s.NotificacaoTipo, s.NotificacaoEntrega, s.VistoNaFilaEm, s.VistoNosAgendadosEm,
            s.Eventos.OrderByDescending(e => e.DataEvento).ThenByDescending(e => e.CapturadoEm)
                .Select(e => new EsusSgEventoDto(e.Id, e.DataEvento, e.Evento, e.TipoEvento, e.EstadoAnterior,
                    e.EstadoAtual, e.UnidadeExecutora, e.Usuario, e.LotacaoEvento, e.Observacao))
                .ToList(),
            detalheAnalise);
    }

    internal static EsusSgSolicitacaoListaDto ParaLista(EsusSgSolicitacao x, AnaliseRegrasResumoDto? analise, DateOnly hoje) => new(
        x.Id, x.IdEsusSg, x.Tipo, x.Recurso, x.DataSolicitacao, x.DataEntradaFila,
        x.DataEntradaFila is { } e && (x.Situacao == SituacaoEsusSg.EmFila || x.Situacao == SituacaoEsusSg.Pendente)
            ? hoje.DayNumber - e.DayNumber
            : null,
        x.Prioridade, x.PrioridadeCor, x.Pendencia, x.PosicaoFila, x.PacienteNome, x.PacienteId,
        x.Cpf, x.Cns, x.DataNascimento, x.UnidadeExecutora, x.DataAgendada, x.DataHoraAgendadaTexto,
        x.NotificacaoResposta, x.Situacao, x.SituacaoAnterior, x.SituacaoMudouEm, x.SincronizadoEm,
        x.EventosCount, analise);
}
