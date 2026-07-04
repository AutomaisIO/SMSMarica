using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Notificacoes.Agendamento.Dtos;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Notificacoes.Agendamento;

/// <summary>
/// Consulta da tela de gestão das notificações de agendamento (painel): lista paginada com
/// status de envio/entrega/leitura, resposta do paciente e motivo; detalhe com linha do tempo;
/// reenvio manual (re-enfileira para o worker, que gera magic link novo).
/// </summary>
public interface IAgendamentoNotificacaoGestaoService
{
    Task<PaginaNotificacoesDto> ListarAsync(NotificacaoFiltroDto filtro, CancellationToken ct = default);
    Task<NotificacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct = default);
    Task ReenviarAsync(Guid id, CancellationToken ct = default);
}

public sealed class AgendamentoNotificacaoGestaoService(
    SmsMaricaDbContext db,
    IPacienteResolver pacienteResolver) : IAgendamentoNotificacaoGestaoService
{
    public async Task<PaginaNotificacoesDto> ListarAsync(NotificacaoFiltroDto filtro, CancellationToken ct = default)
    {
        var query = db.AgendamentoNotificacoes.AsNoTracking()
            .Include(n => n.SolicitacaoExame!).ThenInclude(s => s.TipoExame)
            .Include(n => n.SolicitacaoExame!).ThenInclude(s => s.Unidade)
            .AsQueryable();

        if (Enum.TryParse<StatusNotificacaoAgendamento>(filtro.Status, ignoreCase: true, out var st))
            query = query.Where(n => n.Status == st);
        if (Enum.TryParse<StatusConfirmacaoAgendamento>(filtro.Confirmacao, ignoreCase: true, out var conf))
            query = query.Where(n => n.SolicitacaoExame != null && n.SolicitacaoExame.StatusConfirmacao == conf);
        if (filtro.De is { } de) query = query.Where(n => n.CriadoEm >= de);
        if (filtro.Ate is { } ate) query = query.Where(n => n.CriadoEm < ate.AddDays(1));
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var t = filtro.Texto.Trim();
            query = query.Where(n => n.SolicitacaoExame != null
                && (n.SolicitacaoExame.AccessionNumber.Contains(t)
                    || (n.SolicitacaoExame.CodigoSolicitacao != null && n.SolicitacaoExame.CodigoSolicitacao.Contains(t))
                    || (n.Telefone != null && n.Telefone.Contains(t))));
        }

        var total = await query.CountAsync(ct);
        var pagina = Math.Max(1, filtro.Pagina);
        var tamanho = Math.Clamp(filtro.Tamanho, 1, 200);

        var linhas = await query
            .OrderByDescending(n => n.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(ct);

        var nomes = await pacienteResolver.ResolverManyAsync(linhas.Select(n => n.PacienteId), ct);

        var itens = linhas.Select(n => Mapear(
            n, nomes.TryGetValue(n.PacienteId, out var r) ? r.Nome : null)).ToList();

        return new PaginaNotificacoesDto(itens, total, pagina, tamanho);
    }

    public async Task<NotificacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var n = await db.AgendamentoNotificacoes.AsNoTracking()
            .Include(x => x.SolicitacaoExame!).ThenInclude(s => s.TipoExame)
            .Include(x => x.SolicitacaoExame!).ThenInclude(s => s.Unidade)
            .Include(x => x.MensagemWhatsApp)
            .Include(x => x.LoginLink)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException("notificacao.nao_encontrada", "Notificação não encontrada.");

        var resumo = Mapear(n, (await pacienteResolver.ResolverAsync(n.PacienteId, ct))?.Nome);

        return new NotificacaoDetalheDto(
            resumo,
            n.UltimaTentativaEm,
            n.ProximaTentativaEm,
            n.MensagemWhatsApp?.Conteudo,
            n.MensagemWhatsApp?.Status.ToString(),
            n.MensagemWhatsApp?.ErroMeta,
            n.LoginLink?.ExpiraEm,
            n.LoginLink?.UsadoEm,
            n.LoginLink?.UsadoIp);
    }

    public async Task ReenviarAsync(Guid id, CancellationToken ct = default)
    {
        var n = await db.AgendamentoNotificacoes
            .Include(x => x.SolicitacaoExame)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NaoEncontradoException("notificacao.nao_encontrada", "Notificação não encontrada.");

        if (n.Status == StatusNotificacaoAgendamento.Pendente && n.ProximaTentativaEm is not null)
            throw new ConflitoException("notificacao.ja_na_fila", "Esta notificação já está na fila de envio.");
        if (n.SolicitacaoExame is null || n.SolicitacaoExame.ExcluidoEm is not null)
            throw new ConflitoException("notificacao.sem_solicitacao", "A solicitação desta notificação não existe mais.");
        if (n.SolicitacaoExame.DataAgendada is not { } da || da <= DateTime.UtcNow)
            throw new ConflitoException("notificacao.exame_passado", "O exame já aconteceu — não faz sentido reenviar.");
        if (n.SolicitacaoExame.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
            throw new ConflitoException("notificacao.ja_respondida", "O paciente já respondeu este agendamento.");

        n.Status = StatusNotificacaoAgendamento.Pendente;
        n.MotivoFalha = null;
        n.ProximaTentativaEm = DateTime.UtcNow;
        n.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static NotificacaoResumoDto Mapear(AgendamentoNotificacao n, string? pacienteNome)
    {
        var s = n.SolicitacaoExame;
        return new NotificacaoResumoDto(
            n.Id,
            n.SolicitacaoExameId,
            s?.AccessionNumber,
            s?.CodigoSolicitacao,
            n.PacienteId,
            pacienteNome,
            s?.TipoExame?.Nome,
            s?.Unidade?.Nome,
            s?.DataAgendada,
            n.Telefone,
            n.Status.ToString(),
            n.MotivoFalha,
            n.Tentativas,
            n.EnviadoEm,
            n.EntregueEm,
            n.LidoEm,
            (s?.StatusConfirmacao ?? StatusConfirmacaoAgendamento.Pendente).ToString(),
            s?.ConfirmadoEm,
            s?.ConfirmadoCanal,
            s?.MotivoCancelamentoPaciente,
            n.CriadoEm);
    }
}
