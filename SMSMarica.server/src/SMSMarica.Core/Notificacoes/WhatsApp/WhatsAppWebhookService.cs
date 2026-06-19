using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Tfd;

namespace SMSMarica.Core.Notificacoes.WhatsApp;

public interface IWhatsAppWebhookService
{
    Task ProcessarAsync(string rawJson, CancellationToken ct = default);
}

/// <summary>
/// Processa mensagens recebidas no webhook do WhatsApp: registra (idempotente por
/// <c>wamid</c>) e, quando é resposta de confirmação de acompanhante, atualiza a próxima
/// sessão pendente do paciente (casado pelo telefone).
/// </summary>
public sealed class WhatsAppWebhookService(
    SmsMaricaDbContext db,
    IPacientesService pacientes,
    ILogger<WhatsAppWebhookService> logger) : IWhatsAppWebhookService
{
    public async Task ProcessarAsync(string rawJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch { logger.LogWarning("Webhook WhatsApp: corpo JSON inválido."); return; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes)) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;
                    if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                        continue;
                    foreach (var m in messages.EnumerateArray())
                        await ProcessarMensagemAsync(m, ct);
                }
            }
        }
    }

    private async Task ProcessarMensagemAsync(JsonElement m, CancellationToken ct)
    {
        var waId = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var from = m.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;
        if (string.IsNullOrEmpty(waId) || string.IsNullOrEmpty(from)) return;

        if (await db.MensagensWhatsApp.AsNoTracking().AnyAsync(x => x.WaMessageId == waId, ct))
            return; // idempotência

        var texto = ExtrairTexto(m);
        var contexto = m.TryGetProperty("context", out var ctxEl) && ctxEl.TryGetProperty("id", out var ctxId)
            ? ctxId.GetString() : null;

        var paciente = await pacientes.ObterPorTelefoneAsync(from, ct);

        var msg = new MensagemWhatsApp
        {
            Id = Guid.CreateVersion7(),
            PacienteId = paciente?.Id,
            Telefone = from,
            Direcao = DirecaoMensagem.Entrada,
            Conteudo = texto,
            Status = StatusMensagemWhatsApp.Recebida,
            WaMessageId = waId,
            ContextoWaMessageId = contexto,
            OcorridoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.MensagensWhatsApp.Add(msg);

        var resposta = InterpretarSimNao(texto);
        if (paciente is not null && resposta is not null)
        {
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            var sessao = await (
                from s in db.Sessoes
                join t in db.Tratamentos on s.TratamentoId equals t.Id
                where t.PacienteId == paciente.Id
                    && s.AcompanhanteEsperado == null
                    && s.DataPrevista >= hoje
                    && (s.Status == StatusSessao.Pendente || s.Status == StatusSessao.Confirmada)
                orderby s.DataPrevista
                select s).FirstOrDefaultAsync(ct);

            if (sessao is not null)
            {
                sessao.AcompanhanteEsperado = resposta.Value;
                sessao.AcompanhanteConfirmadoEm = DateTime.UtcNow;
                sessao.AcompanhanteCanal = CanalConfirmacao.WhatsApp;
                sessao.AtualizadoEm = DateTime.UtcNow;
                msg.SessaoId = sessao.Id;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static string? ExtrairTexto(JsonElement m)
    {
        if (m.TryGetProperty("text", out var t) && t.TryGetProperty("body", out var b)) return b.GetString();
        if (m.TryGetProperty("button", out var btn) && btn.TryGetProperty("text", out var bt)) return bt.GetString();
        if (m.TryGetProperty("interactive", out var it))
        {
            if (it.TryGetProperty("button_reply", out var br) && br.TryGetProperty("title", out var brt)) return brt.GetString();
            if (it.TryGetProperty("list_reply", out var lr) && lr.TryGetProperty("title", out var lrt)) return lrt.GetString();
        }
        return null;
    }

    private static bool? InterpretarSimNao(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim().ToLowerInvariant();
        if (t is "1" or "sim" or "s" or "com" || t.Contains("com acompanhante")) return true;
        if (t is "2" or "nao" or "não" or "n" or "sem" || t.Contains("sem acompanhante")) return false;
        return null;
    }
}
