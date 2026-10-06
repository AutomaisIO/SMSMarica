using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Conversas.Midias;
using SMSMais.Core.Integracoes.ElevenLabs;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.WhatsApp.Transcricao;

/// <summary>
/// Passo em segundo plano do STT: pega os áudios marcados <see cref="SituacaoMidiaWhatsApp.TranscrevendoAudio"/>,
/// baixa pelo Zap, transcreve no ElevenLabs e devolve o texto ao fluxo normal de entrada
/// (<see cref="IWhatsAppWebhookService.RotearMensagemTranscritaAsync"/>). Se não der para transcrever
/// (ElevenLabs desligada/sem chave ou falha), marca <see cref="SituacaoMidiaWhatsApp.Falhou"/> e pede
/// por texto — a pessoa nunca fica sem resposta.
/// </summary>
public interface IAudioTranscricaoConversaService
{
    Task<int> ProcessarPendentesAsync(int limite, CancellationToken ct = default);
}

public sealed class AudioTranscricaoConversaService(
    SmsMaisDbContext db,
    IZapMidiaCliente zapMidia,
    IElevenLabsTranscricaoService stt,
    IWhatsAppWebhookService webhook,
    IWhatsAppCliente whatsApp,
    ILogger<AudioTranscricaoConversaService> logger) : IAudioTranscricaoConversaService
{
    // Nota de voz do WhatsApp vai até ~16 MB; folga para não recusar à toa.
    private const long MaxBytes = 25L * 1024 * 1024;

    private const string PecaTexto =
        "No momento não consigo ouvir áudios. Por favor, escreva sua mensagem. 🙏";

    public async Task<int> ProcessarPendentesAsync(int limite, CancellationToken ct = default)
    {
        var pendentes = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.Direcao == DirecaoMensagem.Entrada
                     && m.TipoMensagem == TipoMensagem.Audio
                     && m.MidiaSituacao == SituacaoMidiaWhatsApp.TranscrevendoAudio
                     && m.MidiaWaId != null)
            .OrderBy(m => m.CriadoEm)
            .Take(limite)
            .Select(m => new { m.Id, m.Telefone, m.MidiaWaId, m.MidiaMimeType })
            .ToListAsync(ct);

        foreach (var p in pendentes)
        {
            if (ct.IsCancellationRequested) break;

            var baixada = await zapMidia.BaixarAsync(p.MidiaWaId!, MaxBytes, ct);
            TranscricaoResultado r = baixada.Conteudo is { Length: > 0 }
                ? await stt.TranscreverAsync(baixada.Conteudo, baixada.MimeType ?? p.MidiaMimeType, ct)
                : new TranscricaoResultado(null, baixada.Erro ?? "não foi possível baixar o áudio");

            if (!string.IsNullOrWhiteSpace(r.Texto))
            {
                await webhook.RotearMensagemTranscritaAsync(p.Id, r.Texto!, ct);
            }
            else
            {
                logger.LogInformation("STT não transcreveu a mensagem {Id}: {Erro}", p.Id, r.Erro);
                await MarcarFalhaEResponderAsync(p.Id, p.Telefone, ct);
            }
        }

        return pendentes.Count;
    }

    private async Task MarcarFalhaEResponderAsync(Guid mensagemId, string telefone, CancellationToken ct)
    {
        var msg = await db.MensagensWhatsApp.FirstOrDefaultAsync(m => m.Id == mensagemId, ct);
        if (msg is null || msg.MidiaSituacao != SituacaoMidiaWhatsApp.TranscrevendoAudio) return;
        msg.MidiaSituacao = SituacaoMidiaWhatsApp.Falhou;
        await db.SaveChangesAsync(ct);
        try
        {
            await whatsApp.EnviarTextoAsync(telefone, PecaTexto, msg.PacienteId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao avisar que não consigo ouvir áudios (mensagem {Id}).", mensagemId);
        }
    }
}
