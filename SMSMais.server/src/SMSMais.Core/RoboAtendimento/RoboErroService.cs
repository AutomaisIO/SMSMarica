using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.RoboAtendimento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento;

public interface IRoboErroService
{
    Task RegistrarAsync(RegistrarRoboErroRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<RoboErroDto>> ListarAsync(StatusRoboErro? status, Guid? assuntoId, CancellationToken ct = default);
    Task RevisarAsync(Guid id, RevisarRoboErroRequest request, CancellationToken ct = default);
}

/// <summary>Marca respostas do robô como erradas (por atendentes) e as revisa (treinamento).</summary>
public sealed class RoboErroService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IRoboErroService
{
    private const int Limite = 300;

    public async Task RegistrarAsync(RegistrarRoboErroRequest request, CancellationToken ct = default)
    {
        var conversa = await db.Conversas.AsNoTracking()
            .Where(c => c.Id == request.ConversaId)
            .Select(c => new { c.Id, c.RoboAssuntoId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Conversa", request.ConversaId);

        var me = usuarioAtual.UsuarioId;
        var agora = DateTime.UtcNow;
        var nota = string.IsNullOrWhiteSpace(request.Nota) ? null : request.Nota.Trim();

        string? trecho = null;
        Guid? assuntoId = conversa.RoboAssuntoId;
        if (request.MensagemWhatsAppId is { } msgId)
        {
            var msg = await db.MensagensWhatsApp.AsNoTracking()
                .Where(m => m.Id == msgId && m.ConversaId == request.ConversaId)
                .Select(m => new { m.Conteudo, m.TipoMensagem })
                .FirstOrDefaultAsync(ct)
                ?? throw new NaoEncontradoException("Mensagem", msgId);
            if (msg.TipoMensagem != TipoMensagem.Robo)
                throw new ValidacaoException("mensagemWhatsAppId", "Só é possível marcar erro em mensagem do robô.");
            trecho = msg.Conteudo;

            // Idempotente: já há um erro ABERTO para esta mensagem? Atualiza a nota em vez de duplicar.
            var existente = await db.RoboErrosResposta
                .FirstOrDefaultAsync(e => e.MensagemWhatsAppId == msgId && e.Status == StatusRoboErro.Aberto, ct);
            if (existente is not null)
            {
                if (nota is not null) existente.Nota = nota;
                await db.SaveChangesAsync(ct);
                return;
            }
        }

        db.RoboErrosResposta.Add(new RoboErroResposta
        {
            Id = Guid.CreateVersion7(),
            ConversaId = request.ConversaId,
            MensagemWhatsAppId = request.MensagemWhatsAppId,
            RoboAssuntoId = assuntoId,
            Trecho = trecho,
            Nota = nota,
            Status = StatusRoboErro.Aberto,
            CriadoEm = agora,
            CriadoPor = me,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RoboErroDto>> ListarAsync(
        StatusRoboErro? status, Guid? assuntoId, CancellationToken ct = default)
    {
        var q = db.RoboErrosResposta.AsNoTracking();
        if (status is { } s) q = q.Where(e => e.Status == s);
        if (assuntoId is { } a) q = q.Where(e => e.RoboAssuntoId == a);

        return await q
            .OrderByDescending(e => e.CriadoEm)
            .Take(Limite)
            .Select(e => new RoboErroDto(
                e.Id,
                e.ConversaId,
                e.MensagemWhatsAppId,
                e.RoboAssunto != null ? e.RoboAssunto.Nome : null,
                e.Trecho,
                e.Nota,
                e.Status.ToString(),
                e.CriadoEm,
                db.Usuarios.Where(u => u.Id == e.CriadoPor).Select(u => u.NomeCompleto).FirstOrDefault(),
                e.RevisadoEm,
                e.RevisaoNota))
            .ToListAsync(ct);
    }

    public async Task RevisarAsync(Guid id, RevisarRoboErroRequest request, CancellationToken ct = default)
    {
        var novo = request.Status switch
        {
            "Revisado" => StatusRoboErro.Revisado,
            "Descartado" => StatusRoboErro.Descartado,
            _ => throw new ValidacaoException("status", "Status inválido (use Revisado ou Descartado)."),
        };

        var erro = await db.RoboErrosResposta.FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw new NaoEncontradoException("Erro do robô", id);

        erro.Status = novo;
        erro.RevisadoEm = DateTime.UtcNow;
        erro.RevisadoPor = usuarioAtual.UsuarioId;
        erro.RevisaoNota = string.IsNullOrWhiteSpace(request.Nota) ? null : request.Nota.Trim();
        await db.SaveChangesAsync(ct);
    }
}
