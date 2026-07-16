using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Consultas.Dtos;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Consultas;

/// <summary>
/// Listagem/consulta das SOLICITAÇÕES DE CONSULTA importadas do SISREG — a espinha
/// <c>Solicitacao</c> com <see cref="CategoriaSolicitacao"/> não-imagem (consulta, laboratório,
/// cirurgia, etc.). Sem PACS/laudo/worklist (isso é do exame de imagem). Ver ADR-0021.
/// </summary>
public interface IConsultasService
{
    Task<IReadOnlyList<ConsultaListItemDto>> ListarAsync(FiltroConsultasDto filtro, CancellationToken ct = default);
    Task<ConsultaDetalheDto?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
}

public sealed class ConsultasService(
    SmsMaricaDbContext db,
    IPacienteResolver pacienteResolver) : IConsultasService
{
    public async Task<IReadOnlyList<ConsultaListItemDto>> ListarAsync(
        FiltroConsultasDto filtro, CancellationToken ct = default)
    {
        var query = db.Solicitacoes.AsNoTracking()
            .Include(s => s.UnidadeExecutante)
            .Where(s => s.ExcluidoEm == null && s.Categoria != CategoriaSolicitacao.Imagem);

        if (filtro.PacienteId is { } pid) query = query.Where(s => s.PacienteId == pid);

        var buscaPontual = !string.IsNullOrWhiteSpace(filtro.Busca);
        if (buscaPontual)
        {
            var termo = filtro.Busca!.Trim();
            var padrao = $"%{termo}%";
            var idsPaciente = (await pacienteResolver.BuscarIdsPorTermoAsync(termo, ct)).ToArray();
            query = query.Where(s =>
                (s.CodigoSolicitacao != null && EF.Functions.ILike(s.CodigoSolicitacao, padrao))
                || (s.EspecialidadeTexto != null && EF.Functions.ILike(s.EspecialidadeTexto, padrao))
                || (s.ProcedimentoTexto != null && EF.Functions.ILike(s.ProcedimentoTexto, padrao))
                || idsPaciente.Contains(s.PacienteId));
        }

        // Período pela data agendada (mesma régua da tela de exames). Busca pontual ignora período.
        if (!buscaPontual && filtro.DataInicial.HasValue)
        {
            var ini = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                .AddHours(-FusoBrasilia.OffsetHoras);
            query = query.Where(s => s.DataAgendada != null && s.DataAgendada >= ini);
        }
        if (!buscaPontual && filtro.DataFinal.HasValue)
        {
            var fim = filtro.DataFinal.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
                .AddHours(-FusoBrasilia.OffsetHoras);
            query = query.Where(s => s.DataAgendada != null && s.DataAgendada <= fim);
        }

        var limite = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        var lista = await query
            .OrderByDescending(s => s.DataAgendada)
            .ThenByDescending(s => s.CriadoEm)
            .Take(limite)
            .Select(s => new
            {
                s.Id,
                s.CodigoSolicitacao,
                s.PacienteId,
                s.Categoria,
                s.EspecialidadeTexto,
                s.ProcedimentoTexto,
                UnidadeNome = s.UnidadeExecutante != null ? s.UnidadeExecutante.Nome : string.Empty,
                s.SolicitanteNome,
                s.DataAgendada,
                s.DataSolicitacao,
                s.Status,
                s.StatusConfirmacao,
            })
            .ToListAsync(ct);

        var nomes = await pacienteResolver.ResolverManyAsync(lista.Select(l => l.PacienteId), ct);
        return [.. lista.Select(l => new ConsultaListItemDto(
            l.Id, l.CodigoSolicitacao, l.PacienteId,
            nomes.TryGetValue(l.PacienteId, out var r) ? r.Nome : null,
            l.Categoria.ToString(),
            l.EspecialidadeTexto ?? l.ProcedimentoTexto,
            l.UnidadeNome, l.SolicitanteNome, l.DataAgendada, l.DataSolicitacao,
            l.Status.ToString(), l.StatusConfirmacao.ToString()))];
    }

    public async Task<ConsultaDetalheDto?> ObterPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var s = await db.Solicitacoes.AsNoTracking()
            .Include(x => x.UnidadeExecutante)
            .Include(x => x.UnidadeSolicitante)
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null
                && x.Categoria != CategoriaSolicitacao.Imagem, ct);
        if (s is null) return null;

        var paciente = await pacienteResolver.ResolverAsync(s.PacienteId, ct);
        return new ConsultaDetalheDto(
            s.Id, s.CodigoSolicitacao, s.PacienteId, paciente?.Nome, paciente?.Cpf, paciente?.Cns,
            s.Categoria.ToString(), s.EspecialidadeTexto ?? s.ProcedimentoTexto, s.ProcedimentoTexto,
            s.ProcedimentoSigtapCodigo,
            s.UnidadeExecutante?.Nome ?? string.Empty, s.UnidadeSolicitante?.Nome, s.SolicitanteNome,
            s.DataAgendada, s.DataSolicitacao, s.DataRegulacao,
            s.Status.ToString(), s.StatusConfirmacao.ToString(), s.Observacoes);
    }
}
