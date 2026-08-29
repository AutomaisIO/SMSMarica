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

        // Bloqueio forte por conversa (operador clicou "Parar robô"): vence tudo, inclusive a virada
        // de horário. Só é limpo ao devolver a conversa ao robô.
        if (ctx.Conversa.RoboBloqueado) return;

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

        // Verificação cadastral em andamento idem — a máquina determinística conduz, o robô se cala.
        var temVerificacao = await db.VerificacoesCadastraisEstado.AsNoTracking().AnyAsync(
            e => e.TelefoneCanonical == ctx.Conversa.TelefoneCanonical && e.ExpiraEm > DateTime.UtcNow, ct);
        if (temVerificacao) return;

        // Trava humano: se um humano já respondeu/assumiu desde o corte, cala. Dentro do expediente o
        // corte é a âncora da janela; FORA dele, só a atividade RECENTE cala (o robô assume quando os
        // atendentes saíram, mas recua se um deles acabou de agir — ex.: correção manual às 20h).
        var foraExpediente = TravaHumano.ForaDoExpedienteHumano(
            cfg.HoraAtendimentoHumanoInicio, cfg.HoraAtendimentoHumanoFim, DateTime.UtcNow);
        var ancora = TravaHumano.AncoraEfetiva(
            ctx.Conversa.JanelaAbertaEm, ctx.Mensagem.OcorridoEm, ctx.Conversa.RoboRearmadoEm);
        var corteHumano = TravaHumano.CorteHumano(ancora, foraExpediente, DateTime.UtcNow);
        var humanoRespondeu = await db.MensagensWhatsApp.AsNoTracking().AnyAsync(
            m => m.ConversaId == ctx.Conversa.Id && m.Direcao == DirecaoMensagem.Saida
                && m.AutorUsuarioId != null && m.OcorridoEm >= corteHumano, ct);
        if (humanoRespondeu) return;
        var humanoAssumiu = await db.ConversaEventos.AsNoTracking().AnyAsync(
            e => e.ConversaId == ctx.Conversa.Id && e.AtorUsuarioId != null && e.OcorridoEm >= corteHumano
                && (e.Tipo == TipoEventoConversa.Assumida
                    || e.Tipo == TipoEventoConversa.Transferida
                    || e.Tipo == TipoEventoConversa.EncaminhadaUnidade), ct);
        if (humanoAssumiu) return;

        // NÃO existe limite de interações aqui. Havia um `>= 8` fixo no código, ABAIXO do limite
        // configurável por assunto — e, como o handler nem chega a criar a tarefa, a mensagem sumia
        // sem deixar rastro: o cidadão escrevia e não recebia nada, nem o aviso de passagem. Dois
        // limites concorrentes, e o invisível ganhava.
        //
        // O limite de verdade é `MaxInteracoesSemResolver`, por assunto, aplicado pelo processador —
        // que ainda AVISA o cidadão antes de se calar. Enfileirar é barato: passado o limite, o
        // processador encerra a tarefa sem chamar o modelo, então nada de custo escapa por aqui.

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
