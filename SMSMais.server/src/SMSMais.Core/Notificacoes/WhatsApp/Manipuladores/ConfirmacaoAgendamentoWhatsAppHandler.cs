using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;

/// <summary>
/// Fluxo de confirmação/cancelamento do agendamento de exame pelo WhatsApp:
///
///   quick reply "Não poderei comparecer" (payload <c>confirma:{solicitacaoId}</c>)
///     → pergunta interativa "Deseja realmente cancelar?" (<c>cancela_sim:/cancela_nao:</c>)
///   cancela_nao → StatusConfirmacao=Confirmada (canal whatsapp-quickreply)
///   cancela_sim → pede o motivo (estado AguardandoMotivo)
///   texto livre COM estado AguardandoMotivo ativo → grava motivo + Cancelada
///
/// Sem estado ativo, texto livre passa reto — não sequestra o chat do módulo Conversas.
/// Muta entidades rastreadas (sem SaveChanges); respostas outbound via IWhatsAppCliente
/// (mesmo precedente do AcompanhanteWhatsAppHandler).
/// </summary>
public sealed class ConfirmacaoAgendamentoWhatsAppHandler(
    SmsMaisDbContext db,
    IWhatsAppCliente whatsApp,
    ILogger<ConfirmacaoAgendamentoWhatsAppHandler> logger) : IManipuladorMensagemWhatsApp
{
    private const string PrefixoQuickReply = "confirma:";
    private const string PrefixoCancelaSim = "cancela_sim:";
    private const string PrefixoCancelaNao = "cancela_nao:";
    private static readonly TimeSpan ValidadeEstado = TimeSpan.FromHours(48);

    public int Ordem => 110; // depois do AcompanhanteWhatsAppHandler (100)

    public async Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (TentarExtrairId(ctx.BotaoPayload, PrefixoQuickReply, out var idQuick))
        {
            await TratarNaoPodereiAsync(ctx, idQuick, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoCancelaNao, out var idNao))
        {
            await TratarDesistiuDoCancelamentoAsync(ctx, idNao, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoCancelaSim, out var idSim))
        {
            await TratarConfirmouCancelamentoAsync(ctx, idSim, ct);
            return;
        }

        await TratarTextoLivreComoMotivoAsync(ctx, ct);
    }

    // Quick reply "Não poderei comparecer" do template.
    private async Task TratarNaoPodereiAsync(ManipuladorContexto ctx, Guid solicitacaoId, CancellationToken ct)
    {
        var s = await db.Solicitacoes.FirstOrDefaultAsync(
            x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct);
        if (s is null) return;

        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
        {
            await whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical,
                "Sua resposta para esse agendamento já foi registrada. Se precisar alterar, procure a unidade de saúde. 😊",
                pacienteId: ctx.PacienteId, ct: ct);
            return;
        }

        var notificacao = await db.ComunicacoesPaciente.FirstOrDefaultAsync(
            n => n.SolicitacaoId == solicitacaoId && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
        if (notificacao is null) return;

        await AbrirOuAtualizarEstadoAsync(ctx.Conversa.TelefoneCanonical, notificacao.Id,
            EtapaConfirmacaoAgendamento.AguardandoConfirmacaoCancelamento, ct);

        await whatsApp.EnviarInterativoBotoesAsync(
            ctx.Conversa.TelefoneCanonical,
            "Entendi! Você quer mesmo CANCELAR sua presença nesse exame?",
            [
                new BotaoInterativoWhatsApp($"{PrefixoCancelaSim}{solicitacaoId}", "Sim, cancelar"),
                new BotaoInterativoWhatsApp($"{PrefixoCancelaNao}{solicitacaoId}", "Não, vou comparecer"),
            ],
            pacienteId: ctx.PacienteId, ct: ct);
    }

    // "Não, vou comparecer" — vira confirmação.
    private async Task TratarDesistiuDoCancelamentoAsync(ManipuladorContexto ctx, Guid solicitacaoId, CancellationToken ct)
    {
        var s = await db.Solicitacoes.FirstOrDefaultAsync(
            x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct);
        if (s is null) return;

        await RemoverEstadoAsync(ctx.Conversa.TelefoneCanonical, ct);

        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente)
        {
            Confirmar(s, "whatsapp-quickreply");
            await whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical,
                "Perfeito, presença confirmada! ✅ Até lá. 😊", pacienteId: ctx.PacienteId, ct: ct);
        }
    }

    // "Sim, cancelar" — pede o motivo.
    private async Task TratarConfirmouCancelamentoAsync(ManipuladorContexto ctx, Guid solicitacaoId, CancellationToken ct)
    {
        var notificacao = await db.ComunicacoesPaciente.FirstOrDefaultAsync(
            n => n.SolicitacaoId == solicitacaoId && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
        if (notificacao is null) return;

        await AbrirOuAtualizarEstadoAsync(ctx.Conversa.TelefoneCanonical, notificacao.Id,
            EtapaConfirmacaoAgendamento.AguardandoMotivo, ct);

        await whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical,
            "Tudo bem. Pode me dizer o motivo? Assim conseguimos oferecer a vaga a outra pessoa.",
            pacienteId: ctx.PacienteId, ct: ct);
    }

    // Texto livre só é motivo quando há estado AguardandoMotivo ativo para o telefone.
    private async Task TratarTextoLivreComoMotivoAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ctx.Texto)) return;
        // Toque em botão nunca é motivo — ex.: "Falar com atendente" (payload atendente:)
        // durante o AguardandoMotivo deve seguir para a Central de Atendimento, não cancelar.
        if (!string.IsNullOrEmpty(ctx.BotaoPayload)) return;

        var estado = await db.AgendamentoConfirmacaoEstados
            .Include(e => e.ComunicacaoPaciente)
            .FirstOrDefaultAsync(e => e.TelefoneCanonical == ctx.Conversa.TelefoneCanonical, ct);
        if (estado is null) return;

        if (estado.ExpiraEm <= DateTime.UtcNow)
        {
            db.AgendamentoConfirmacaoEstados.Remove(estado);
            return;
        }
        if (estado.Etapa != EtapaConfirmacaoAgendamento.AguardandoMotivo) return;

        // A partir daqui o texto livre é tratado como o motivo — o robô não responde por cima.
        ctx.Consumido = true;
        var solicitacaoId = estado.ComunicacaoPaciente?.SolicitacaoId;
        if (solicitacaoId is null) { db.AgendamentoConfirmacaoEstados.Remove(estado); return; }

        var s = await db.Solicitacoes.FirstOrDefaultAsync(
            x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct);
        db.AgendamentoConfirmacaoEstados.Remove(estado);
        if (s is null) return;

        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente)
        {
            var motivo = ctx.Texto.Trim();
            s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
            s.ConfirmacaoCanceladaEm = DateTime.UtcNow;
            s.ConfirmadoCanal = "whatsapp-quickreply";
            s.MotivoCancelamentoPaciente = motivo.Length <= 500 ? motivo : motivo[..500];
            s.AtualizadoEm = DateTime.UtcNow;

            logger.LogInformation("Paciente avisou que não irá ao exame {Id} via WhatsApp.", s.Id);
            await whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical,
                "Obrigado por avisar! 🙏 Registramos que você não poderá comparecer; a equipe da unidade vai reavaliar a vaga.",
                pacienteId: ctx.PacienteId, ct: ct);
        }
    }

    private static void Confirmar(Solicitacao s, string canal)
    {
        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
        s.ConfirmadoEm = DateTime.UtcNow;
        s.ConfirmadoCanal = canal;
        s.AtualizadoEm = DateTime.UtcNow;
    }

    private async Task AbrirOuAtualizarEstadoAsync(
        string telefone, Guid notificacaoId, EtapaConfirmacaoAgendamento etapa, CancellationToken ct)
    {
        var estado = await db.AgendamentoConfirmacaoEstados
            .FirstOrDefaultAsync(e => e.TelefoneCanonical == telefone, ct);
        if (estado is null)
        {
            db.AgendamentoConfirmacaoEstados.Add(new AgendamentoConfirmacaoEstado
            {
                Id = Guid.CreateVersion7(),
                TelefoneCanonical = telefone,
                ComunicacaoPacienteId = notificacaoId,
                Etapa = etapa,
                ExpiraEm = DateTime.UtcNow.Add(ValidadeEstado),
                CriadoEm = DateTime.UtcNow,
            });
            return;
        }
        estado.ComunicacaoPacienteId = notificacaoId;
        estado.Etapa = etapa;
        estado.ExpiraEm = DateTime.UtcNow.Add(ValidadeEstado);
        estado.AtualizadoEm = DateTime.UtcNow;
    }

    private async Task RemoverEstadoAsync(string telefone, CancellationToken ct)
    {
        var estado = await db.AgendamentoConfirmacaoEstados
            .FirstOrDefaultAsync(e => e.TelefoneCanonical == telefone, ct);
        if (estado is not null) db.AgendamentoConfirmacaoEstados.Remove(estado);
    }

    private static bool TentarExtrairId(string? valor, string prefixo, out Guid id)
    {
        id = default;
        return valor is not null
            && valor.StartsWith(prefixo, StringComparison.Ordinal)
            && Guid.TryParse(valor[prefixo.Length..], out id);
    }
}
