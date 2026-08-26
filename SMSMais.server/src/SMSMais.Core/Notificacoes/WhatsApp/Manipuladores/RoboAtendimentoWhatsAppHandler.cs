using Microsoft.EntityFrameworkCore;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;

/// <summary>
/// Robô de atendimento: quando nenhum fluxo determinístico tratou a mensagem e nenhum humano
/// está na janela, ENFILEIRA uma tarefa (robo_tarefa) para o worker responder fora do webhook.
/// Roda por último (Ordem alta). NÃO chama SaveChanges — o webhook commita.
/// </summary>
public sealed class RoboAtendimentoWhatsAppHandler(SmsMaisDbContext db) : IManipuladorMensagemWhatsApp
{
    public int Ordem => 1000; // depois de todos os fluxos de domínio.

    public async Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (ctx.Consumido) return;

        var ehAtendente = ctx.BotaoPayload?.StartsWith("atendente:", StringComparison.Ordinal) == true;
        // Botões/replies de outros domínios não são do robô; e sem texto (a menos que seja o botão atendente) não há o que tratar.
        if (!ehAtendente && !string.IsNullOrEmpty(ctx.BotaoPayload)) return;
        if (!string.IsNullOrEmpty(ctx.InterativoReplyId)) return;
        if (!ehAtendente && string.IsNullOrWhiteSpace(ctx.Texto)) return;

        // Robô ligado globalmente? (e o expediente humano, para o override por horário)
        var cfg = await db.RoboConfiguracoes.AsNoTracking()
            .Select(c => new { c.Ativo, c.HoraAtendimentoHumanoInicio, c.HoraAtendimentoHumanoFim })
            .FirstOrDefaultAsync(ct);
        if (cfg is not { Ativo: true }) return;

        // Diálogo de confirmação em andamento (Sim/Não/motivo) é do fluxo determinístico — não interferir.
        var temEstadoConfirmacao = await db.AgendamentoConfirmacaoEstados.AsNoTracking().AnyAsync(
            e => e.TelefoneCanonical == ctx.Conversa.TelefoneCanonical && e.ExpiraEm > DateTime.UtcNow, ct);
        if (temEstadoConfirmacao) return;

        // Trava humano-por-janela: se um humano já respondeu/assumiu desde a âncora, cala — salvo
        // FORA do expediente dos atendentes (antes de abrir / depois de fechar), quando o robô assume.
        var ignorarHumano = TravaHumano.ForaDoExpedienteHumano(
            cfg.HoraAtendimentoHumanoInicio, cfg.HoraAtendimentoHumanoFim, DateTime.UtcNow);
        if (!ignorarHumano)
        {
            var ancora = TravaHumano.AncoraEfetiva(
                ctx.Conversa.JanelaAbertaEm, ctx.Mensagem.OcorridoEm, ctx.Conversa.RoboRearmadoEm);
            var humanoRespondeu = await db.MensagensWhatsApp.AsNoTracking().AnyAsync(
                m => m.ConversaId == ctx.Conversa.Id && m.Direcao == DirecaoMensagem.Saida
                    && m.AutorUsuarioId != null && m.OcorridoEm >= ancora, ct);
            if (humanoRespondeu) return;
            var humanoAssumiu = await db.ConversaEventos.AsNoTracking().AnyAsync(
                e => e.ConversaId == ctx.Conversa.Id && e.AtorUsuarioId != null && e.OcorridoEm >= ancora
                    && (e.Tipo == TipoEventoConversa.Assumida
                        || e.Tipo == TipoEventoConversa.Transferida
                        || e.Tipo == TipoEventoConversa.EncaminhadaUnidade), ct);
            if (humanoAssumiu) return;
        }

        // Limite de interações do robô na janela.
        if (ctx.Conversa.RoboInteracoesNaJanela >= 8) return;

        db.RoboTarefas.Add(new RoboAtendimentoTarefa
        {
            Id = Guid.CreateVersion7(),
            ConversaId = ctx.Conversa.Id,
            MensagemWhatsAppId = ctx.Mensagem.Id,
            PacienteId = ctx.PacienteId,
            Status = StatusRoboTarefa.Pendente,
            CriadoEm = DateTime.UtcNow,
        });
    }
}
