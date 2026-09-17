using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

/// <summary>
/// Leituras do menu Confirmações: fotografia da fila, respostas dos pacientes (confirmou / não
/// vai, com o motivo) e as chaves por unidade. A lista detalhada da fila reaproveita
/// <see cref="Comunicacao.IComunicacaoGestaoService"/> filtrada pela finalidade confirmação.
/// </summary>
public interface IConfirmacoesPainelService
{
    Task<ResumoFilaConfirmacaoDto> ResumoFilaAsync(CancellationToken ct = default);

    Task<PaginaRespostasConfirmacaoDto> ListarRespostasAsync(
        string? resposta, DateTime? de, DateTime? ate, string? texto, int pagina, int tamanho,
        CancellationToken ct = default);

    Task<IReadOnlyList<RegraUnidadeConfirmacaoDto>> ListarRegrasUnidadesAsync(CancellationToken ct = default);

    /// <summary>Liga/desliga o aviso por WhatsApp da unidade (mesma chave do mapeamento SISREG).</summary>
    Task AlterarRegraUnidadeAsync(Guid unidadeId, bool enviar, CancellationToken ct = default);
}

public sealed class ConfirmacoesPainelService(
    SmsMaisDbContext db,
    IConfirmacaoConfiguracaoService configuracao,
    IPacienteResolver pacienteResolver) : IConfirmacoesPainelService
{
    public async Task<ResumoFilaConfirmacaoDto> ResumoFilaAsync(CancellationToken ct = default)
    {
        var cfg = await configuracao.ObterAsync(ct);
        var inicio = TimeOnly.Parse(cfg.HoraInicioEnvio);
        var fim = TimeOnly.Parse(cfg.HoraFimEnvio);
        var agora = DateTime.UtcNow;
        var hoje = FusoBrasilia.InicioDoDiaAtualEmUtc();

        var porStatus = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Qtd = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Qtd, ct);
        int Qtd(StatusComunicacao s) => porStatus.GetValueOrDefault(s);

        var fila = db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                && c.Status == StatusComunicacao.Pendente && c.ProximaTentativaEm != null);
        var naFila = await fila.CountAsync(ct);
        var prontas = await fila.CountAsync(c => c.ProximaTentativaEm <= agora, ct);

        var enviadasHoje = await db.ComunicacoesPaciente.AsNoTracking()
            .CountAsync(c => c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                && c.EnviadoEm >= hoje, ct);
        var confirmadasHoje = await db.Solicitacoes.AsNoTracking()
            .CountAsync(s => s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada
                && s.ConfirmadoEm >= hoje, ct);
        var canceladasHoje = await db.Solicitacoes.AsNoTracking()
            .CountAsync(s => s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada
                && s.ConfirmacaoCanceladaEm >= hoje, ct);

        return new ResumoFilaConfirmacaoDto(
            cfg.JanelaAbertaAgora, cfg.HoraInicioEnvio, cfg.HoraFimEnvio,
            JanelaEnvioConfirmacao.ProximaAbertura(agora, inicio, fim),
            naFila, prontas,
            Qtd(StatusComunicacao.AguardandoVerificacaoCadastral),
            Qtd(StatusComunicacao.AguardandoCorrecaoContato),
            Qtd(StatusComunicacao.SemTelefoneValido),
            Qtd(StatusComunicacao.Falha),
            enviadasHoje, confirmadasHoje, canceladasHoje);
    }

    public async Task<PaginaRespostasConfirmacaoDto> ListarRespostasAsync(
        string? resposta, DateTime? de, DateTime? ate, string? texto, int pagina, int tamanho,
        CancellationToken ct = default)
    {
        var query = db.Solicitacoes.AsNoTracking()
            .Where(s => s.ExcluidoEm == null && s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente);

        if (Enum.TryParse<StatusConfirmacaoAgendamento>(resposta, ignoreCase: true, out var r)
            && r != StatusConfirmacaoAgendamento.Pendente)
            query = query.Where(s => s.StatusConfirmacao == r);

        // "Respondido em" = quando confirmou OU quando avisou que não vai.
        if (de is { } d)
            query = query.Where(s => (s.ConfirmacaoCanceladaEm ?? s.ConfirmadoEm) >= d);
        if (ate is { } a)
            query = query.Where(s => (s.ConfirmacaoCanceladaEm ?? s.ConfirmadoEm) < a.AddDays(1));

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim();
            var ids = await pacienteResolver.BuscarIdsPorTermoAsync(t, 200, ct);
            query = query.Where(s => ids.Contains(s.PacienteId)
                || (s.CodigoSolicitacao != null && s.CodigoSolicitacao.Contains(t)));
        }

        var total = await query.CountAsync(ct);
        pagina = Math.Max(1, pagina);
        tamanho = Math.Clamp(tamanho, 1, 200);

        var linhas = await query
            .OrderByDescending(s => s.ConfirmacaoCanceladaEm ?? s.ConfirmadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(s => new
            {
                s.Id,
                ExameId = s.ExameImagem != null ? (Guid?)s.ExameImagem.Id : null,
                s.CodigoSolicitacao,
                s.PacienteId,
                s.Categoria,
                Procedimento = s.ExameImagem != null && s.ExameImagem.TipoExame != null
                    ? s.ExameImagem.TipoExame.Nome
                    : s.EspecialidadeTexto ?? s.ProcedimentoTexto,
                Unidade = s.UnidadeExecutante != null ? s.UnidadeExecutante.Nome : null,
                s.DataAgendada,
                s.StatusConfirmacao,
                s.ConfirmadoCanal,
                s.ConfirmadoEm,
                s.ConfirmacaoCanceladaEm,
                s.MotivoCancelamentoPaciente,
                s.Status,
            })
            .ToListAsync(ct);

        var nomes = await pacienteResolver.ResolverManyAsync(linhas.Select(l => l.PacienteId), ct);

        return new PaginaRespostasConfirmacaoDto(
            [.. linhas.Select(l => new RespostaConfirmacaoDto(
                l.Id, l.ExameId, l.CodigoSolicitacao, l.PacienteId,
                nomes.TryGetValue(l.PacienteId, out var n) ? n.Nome : null,
                l.Categoria.ToString(), l.Procedimento, l.Unidade, l.DataAgendada,
                l.StatusConfirmacao.ToString(), l.ConfirmadoCanal,
                l.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada ? l.ConfirmacaoCanceladaEm : l.ConfirmadoEm,
                l.MotivoCancelamentoPaciente,
                l.Status.ToString()))],
            total, pagina, tamanho);
    }

    public async Task AlterarRegraUnidadeAsync(Guid unidadeId, bool enviar, CancellationToken ct = default)
    {
        if (!await db.Unidades.AnyAsync(u => u.Id == unidadeId, ct))
            throw new Common.Excecoes.NaoEncontradoException("Unidade", unidadeId);

        var agenda = await db.SisregVarreduraAgendas.FirstOrDefaultAsync(a => a.UnidadeId == unidadeId, ct);
        if (agenda is null)
        {
            // Sem agenda: nasce com a varredura DESLIGADA (ProximoRunEm nulo = não elegível) — aqui
            // só se decide o aviso ao paciente, não se liga varredura de ninguém.
            agenda = new Data.Entities.Sisreg.SisregVarreduraAgenda { UnidadeId = unidadeId, Ativo = false };
            db.SisregVarreduraAgendas.Add(agenda);
        }
        agenda.EnviarConfirmacao = enviar;
        agenda.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RegraUnidadeConfirmacaoDto>> ListarRegrasUnidadesAsync(CancellationToken ct = default)
    {
        var agendas = await db.SisregVarreduraAgendas.AsNoTracking()
            .Select(a => new { a.UnidadeId, a.EnviarConfirmacao })
            .ToListAsync(ct);

        // Procedimentos distintos (por código) mapeados na unidade, e quantos avisam o paciente.
        var procs = await db.SisregProcedimentosProfissional.AsNoTracking()
            .Where(p => p.Profissional != null)
            .Select(p => new { p.Profissional!.UnidadeId, p.Codigo, p.EnviarConfirmacao })
            .Distinct()
            .ToListAsync(ct);
        var porUnidade = procs.GroupBy(p => p.UnidadeId).ToDictionary(
            g => g.Key,
            g => (Total: g.Select(x => x.Codigo).Distinct().Count(),
                  ComAviso: g.Where(x => x.EnviarConfirmacao).Select(x => x.Codigo).Distinct().Count()));

        var unidadeIds = agendas.Select(a => a.UnidadeId).Union(porUnidade.Keys).ToList();
        var nomes = await db.Unidades.AsNoTracking()
            .Where(u => unidadeIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, ct);

        return [.. unidadeIds
            .Select(id => new RegraUnidadeConfirmacaoDto(
                id,
                nomes.GetValueOrDefault(id) ?? "(unidade removida)",
                agendas.FirstOrDefault(a => a.UnidadeId == id)?.EnviarConfirmacao ?? false,
                porUnidade.TryGetValue(id, out var p) ? p.ComAviso : 0,
                porUnidade.TryGetValue(id, out var q) ? q.Total : 0))
            .OrderByDescending(x => x.EnviarConfirmacao)
            .ThenBy(x => x.UnidadeNome)];
    }
}
