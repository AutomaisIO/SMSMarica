using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Consultas.Dtos;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
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
    IPacienteResolver pacienteResolver,
    IUsuarioAtualAccessor usuarioAtual) : IConsultasService
{
    public async Task<IReadOnlyList<ConsultaListItemDto>> ListarAsync(
        FiltroConsultasDto filtro, CancellationToken ct = default)
    {
        var query = db.Solicitacoes.AsNoTracking()
            .Include(s => s.UnidadeExecutante)
            .Where(s => s.ExcluidoEm == null && s.Categoria != CategoriaSolicitacao.Imagem);

        if (filtro.PacienteId is { } pid) query = query.Where(s => s.PacienteId == pid);
        if (filtro.Status is { } st) query = query.Where(s => s.Status == st);

        // Multitenancy por unidade (mesmo escopo dos exames): só as consultas cuja EXECUTORA ou
        // SOLICITANTE está na(s) unidade(s) do usuário. Devolve a unidade de referência p/ a seta.
        Guid? unidadeReferencia;
        (query, unidadeReferencia) = await AplicarEscopoUnidadeAsync(query, ct);

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
                s.UnidadeExecutanteId,
                s.UnidadeSolicitanteId,
                s.SolicitanteNome,
                s.DataAgendada,
                s.DataSolicitacao,
                s.Status,
                s.StatusConfirmacao,
            })
            .ToListAsync(ct);

        var nomes = await pacienteResolver.ResolverManyAsync(lista.Select(l => l.PacienteId), ct);

        // Chip da confirmação por WhatsApp (mesmos checks da lista de exames). Aqui o id da linha
        // JÁ É o da espinha, então a comunicação casa direto por SolicitacaoId — sem o mapa
        // exame→solicitação que os exames precisam.
        var ids = lista.Select(l => l.Id).ToArray();
        var chips = (await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.SolicitacaoId != null && ids.Contains(c.SolicitacaoId.Value)
                && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
            .Select(c => new { c.SolicitacaoId, c.Status, c.VisualizadoEm, c.MotivoFalha })
            .ToListAsync(ct))
            .ToDictionary(
                c => c.SolicitacaoId!.Value,
                c => new ComunicacaoChipDto(c.Status.ToString(), c.VisualizadoEm != null, c.MotivoFalha));

        // Direção relativa à unidade de referência: executora → Recebida; solicitante → Enviada.
        // Mesma regra dos exames (Recebida prevalece quando a unidade é as duas coisas).
        DirecaoSolicitacao? Direcao(Guid execId, Guid? solicId) =>
            unidadeReferencia is { } r
                ? execId == r ? DirecaoSolicitacao.Recebida
                : solicId == r ? DirecaoSolicitacao.Enviada
                : null
                : null;

        return [.. lista.Select(l => new ConsultaListItemDto(
            l.Id, l.CodigoSolicitacao, l.PacienteId,
            nomes.TryGetValue(l.PacienteId, out var r) ? r.Nome : null,
            l.Categoria.ToString(),
            l.EspecialidadeTexto ?? l.ProcedimentoTexto,
            l.UnidadeNome, l.SolicitanteNome, l.DataAgendada, l.DataSolicitacao,
            l.Status.ToString(), l.StatusConfirmacao.ToString(),
            chips.GetValueOrDefault(l.Id),
            Direcao(l.UnidadeExecutanteId, l.UnidadeSolicitanteId)))];
    }

    /// <summary>
    /// Multitenancy por unidade — igual ao dos exames, mas sobre a espinha <c>Solicitacao</c>
    /// diretamente (consulta não tem satélite). Restringe às unidades vinculadas ao usuário
    /// (executora OU solicitante). Usuário sem vínculo/contexto vê tudo. Devolve a unidade de
    /// referência (a ativa resolvida, ou null na visão do conjunto) — marca a direção da seta.
    /// </summary>
    private async Task<(IQueryable<Solicitacao> Query, Guid? UnidadeReferencia)> AplicarEscopoUnidadeAsync(
        IQueryable<Solicitacao> query, CancellationToken ct)
    {
        var usuarioId = usuarioAtual.UsuarioId;
        if (usuarioId is null) return (query, null);

        var ativa = usuarioAtual.UnidadeAtivaId;

        // Global admin: vínculo implícito a TODAS as unidades; a ativa vira filtro de conveniência.
        if (usuarioId == IdentificadoresFixos.UsuarioAdminId)
        {
            if (ativa.HasValue &&
                await db.Unidades.AsNoTracking().AnyAsync(u => u.Id == ativa.Value && u.Ativo, ct))
            {
                return (query.Where(s => s.UnidadeExecutanteId == ativa.Value
                    || s.UnidadeSolicitanteId == ativa.Value), ativa);
            }
            return (query, null);
        }

        var vinculos = await db.UsuarioUnidades.AsNoTracking()
            .Where(v => v.UsuarioId == usuarioId && v.Unidade!.Ativo)
            .Select(v => v.UnidadeId)
            .ToArrayAsync(ct);
        if (vinculos.Length == 0) return (query, null);

        if (ativa.HasValue && vinculos.Contains(ativa.Value))
        {
            return (query.Where(s => s.UnidadeExecutanteId == ativa.Value
                || s.UnidadeSolicitanteId == ativa.Value), ativa);
        }

        // Visão do conjunto: executora OU solicitante entre as vinculadas. Sem referência → sem seta.
        return (
            query.Where(s => vinculos.Contains(s.UnidadeExecutanteId)
                || (s.UnidadeSolicitanteId != null && vinculos.Contains(s.UnidadeSolicitanteId.Value))),
            null);
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
            s.Status.ToString(), s.StatusConfirmacao.ToString(), s.Observacoes, s.RawSisreg);
    }
}
