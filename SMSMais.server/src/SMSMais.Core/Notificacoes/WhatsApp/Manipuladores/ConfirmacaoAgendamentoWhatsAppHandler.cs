using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.VerificacaoCadastral;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;

/// <summary>
/// Fluxo de confirmação/cancelamento do agendamento pelo WhatsApp:
///
///   quick reply "Não poderei ir!" (payload <c>confirma:{solicitacaoId}</c>)
///     → pergunta com os botões "Quero cancelar" / "Não quero cancelar"
///       (<c>cancela_sim:/cancela_nao:</c>) — botões que se leem sem depender de pontuação
///       ("Não, vou comparecer" confundia quem lê rápido)
///   "Não quero cancelar" → StatusConfirmacao=Confirmada (canal whatsapp-quickreply)
///   "Quero cancelar"     → StatusConfirmacao=Cancelada NA HORA, e pede o motivo (opcional)
///   texto livre COM estado AguardandoMotivo ativo → grava o motivo no cancelamento já feito
///
/// Antes o cancelamento só valia depois do motivo: quem tocava "cancelar" e não escrevia nada
/// ficava como "sem resposta" e a unidade nunca sabia que a vaga ia sobrar.
/// Sem estado ativo, texto livre passa reto — não sequestra o chat do módulo Conversas.
/// Muta entidades rastreadas (sem SaveChanges); respostas outbound via IWhatsAppCliente.
/// Nada daqui vai para o SISREG: a resposta do paciente vive só no nosso sistema.
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
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O que o paciente precisa fazer — o mesmo que o template da regulação pede, dito
    /// de forma direta. É o recado que não pode se perder.</summary>
    internal const string LembreteGuia =
        "⚠️ *IMPORTANTE:* antes do dia, passe no *posto de saúde* onde você é atendido(a) para "
        + "retirar a *guia (ficha de solicitação)*. Sem ela não é possível fazer o atendimento.\n\n"
        + "No dia, leve: a *guia*, o *pedido médico*, o *cartão do SUS* e o *comprovante de residência*.";

    public int Ordem => 110; // depois do AcompanhanteWhatsAppHandler (100)

    public async Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (TentarExtrairId(ctx.BotaoPayload, PrefixoQuickReply, out var idQuick))
        {
            ctx.Consumido = true;
            await TratarNaoPodereiAsync(ctx, idQuick, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoCancelaNao, out var idNao))
        {
            ctx.Consumido = true;
            await TratarNaoQueroCancelarAsync(ctx, idNao, ct);
            return;
        }
        if (TentarExtrairId(ctx.InterativoReplyId, PrefixoCancelaSim, out var idSim))
        {
            ctx.Consumido = true;
            await TratarQueroCancelarAsync(ctx, idSim, ct);
            return;
        }

        // Botões do LEMBRETE (agendamento_proximo): quick replies sem payload, voltam como TEXTO.
        // "Sim! Está confirmado!" e "Não poderei ir." — o agendamento é o da mensagem respondida.
        if (string.IsNullOrEmpty(ctx.InterativoReplyId))
        {
            if (InterpretadorRespostaCidadao.ConfirmaComparecimento(ctx.Texto)
                && await SolicitacaoDoLembreteAsync(ctx, ct) is { } idConfirma)
            {
                ctx.Consumido = true;
                await TratarSegueConfirmadoAsync(ctx, idConfirma, ct);
                return;
            }
            if (InterpretadorRespostaCidadao.NaoPodereiIr(ctx.Texto)
                && await SolicitacaoDoLembreteAsync(ctx, ct) is { } idNaoVai)
            {
                ctx.Consumido = true;
                await TratarNaoPodereiAsync(ctx, idNaoVai, ct);
                return;
            }
        }

        await TratarTextoLivreComoMotivoAsync(ctx, ct);
    }

    /// <summary>
    /// De qual agendamento a pessoa está falando? Do que a mensagem RESPONDE (a Meta manda o
    /// contexto) — e, sem contexto, do lembrete mais recente enviado para aquele número. Sem isso,
    /// "Não poderei ir." de quem tem dois exames marcados cancelaria o errado.
    /// </summary>
    private async Task<Guid?> SolicitacaoDoLembreteAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (ctx.Mensagem.ContextoWaMessageId is { } wamid)
        {
            var porContexto = await db.ComunicacoesPaciente.AsNoTracking()
                .Where(c => c.MensagemWhatsApp != null && c.MensagemWhatsApp.WaMessageId == wamid
                    && c.SolicitacaoId != null)
                .Select(c => c.SolicitacaoId)
                .FirstOrDefaultAsync(ct);
            if (porContexto is not null) return porContexto;
        }

        var telefone = ctx.Conversa.TelefoneCanonical;
        var recentes = await db.ComunicacoesPaciente.AsNoTracking()
            .Where(c => c.Finalidade == FinalidadeComunicacao.LembreteAgendamento
                && c.SolicitacaoId != null && c.Telefone != null && c.EnviadoEm != null
                && c.EnviadoEm > DateTime.UtcNow.AddDays(-30))
            .OrderByDescending(c => c.EnviadoEm)
            .Select(c => new { c.SolicitacaoId, c.Telefone })
            .Take(50)
            .ToListAsync(ct);

        return recentes
            .FirstOrDefault(c => Conversas.TelefoneWhatsApp.MesmoNumero(c.Telefone, telefone))
            ?.SolicitacaoId;
    }

    /// <summary>"Sim! Está confirmado!" do lembrete: confirma (ou só agradece, se já estava).</summary>
    private async Task TratarSegueConfirmadoAsync(ManipuladorContexto ctx, Guid solicitacaoId, CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);
        if (s is null) return;

        await RemoverEstadoAsync(ctx.Conversa.TelefoneCanonical, ct);

        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente)
        {
            s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
            s.ConfirmadoEm = DateTime.UtcNow;
            s.ConfirmadoCanal = "whatsapp-quickreply";
            s.AtualizadoEm = DateTime.UtcNow;
        }
        else if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada)
        {
            // Tinha avisado que não ia e agora diz que vai: a presença volta a valer.
            s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
            s.ConfirmadoEm = DateTime.UtcNow;
            s.ConfirmadoCanal = "whatsapp-quickreply";
            s.ConfirmacaoCanceladaEm = null;
            s.MotivoCancelamentoPaciente = null;
            s.AtualizadoEm = DateTime.UtcNow;
        }

        await whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical,
            $"Combinado! Sua presença {DescricaoAgendamento(s)} está *CONFIRMADA* ✅\n\n{LembreteGuia}",
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    // Quick reply "Não poderei ir!" do template.
    private async Task TratarNaoPodereiAsync(ManipuladorContexto ctx, Guid solicitacaoId, CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);
        if (s is null) return;

        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
        {
            await ResponderJaRegistradaAsync(ctx, s, ct);
            return;
        }

        var notificacao = await db.ComunicacoesPaciente.FirstOrDefaultAsync(
            n => n.SolicitacaoId == solicitacaoId && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento, ct);
        if (notificacao is null) return;

        await AbrirOuAtualizarEstadoAsync(ctx.Conversa.TelefoneCanonical, notificacao.Id,
            EtapaConfirmacaoAgendamento.AguardandoConfirmacaoCancelamento, ct);

        await whatsApp.EnviarInterativoBotoesAsync(
            ctx.Conversa.TelefoneCanonical,
            $"Você quer *CANCELAR* sua presença {DescricaoAgendamento(s)}?\n\nToque em um dos botões abaixo.",
            [
                new BotaoInterativoWhatsApp($"{PrefixoCancelaSim}{solicitacaoId}", "Quero cancelar"),
                new BotaoInterativoWhatsApp($"{PrefixoCancelaNao}{solicitacaoId}", "Não quero cancelar"),
            ],
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    // "Não quero cancelar" — vira confirmação de presença.
    private async Task TratarNaoQueroCancelarAsync(ManipuladorContexto ctx, Guid solicitacaoId, CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);
        if (s is null) return;

        await RemoverEstadoAsync(ctx.Conversa.TelefoneCanonical, ct);

        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
        {
            await ResponderJaRegistradaAsync(ctx, s, ct);
            return;
        }

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
        s.ConfirmadoEm = DateTime.UtcNow;
        s.ConfirmadoCanal = "whatsapp-quickreply";
        s.AtualizadoEm = DateTime.UtcNow;

        await whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical,
            $"Combinado! Sua presença {DescricaoAgendamento(s)} está *CONFIRMADA* ✅\n\n{LembreteGuia}",
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    // "Quero cancelar" — registra o cancelamento já; o motivo é opcional e vem depois.
    private async Task TratarQueroCancelarAsync(ManipuladorContexto ctx, Guid solicitacaoId, CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);
        if (s is null) return;

        if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Pendente)
        {
            await RemoverEstadoAsync(ctx.Conversa.TelefoneCanonical, ct);
            await ResponderJaRegistradaAsync(ctx, s, ct);
            return;
        }

        var notificacao = await db.ComunicacoesPaciente.FirstOrDefaultAsync(
            n => n.SolicitacaoId == solicitacaoId && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento, ct);

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
        s.ConfirmacaoCanceladaEm = DateTime.UtcNow;
        s.ConfirmadoCanal = "whatsapp-quickreply";
        s.MotivoCancelamentoPaciente = null;
        s.AtualizadoEm = DateTime.UtcNow;
        logger.LogInformation("Paciente avisou que não irá ao agendamento {Id} via WhatsApp.", s.Id);

        if (notificacao is not null)
            await AbrirOuAtualizarEstadoAsync(ctx.Conversa.TelefoneCanonical, notificacao.Id,
                EtapaConfirmacaoAgendamento.AguardandoMotivo, ct);
        else
            await RemoverEstadoAsync(ctx.Conversa.TelefoneCanonical, ct);

        await whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical,
            $"Pronto. Registramos que você *NÃO VAI* {DescricaoAgendamento(s, cancelamento: true)}. "
            + "Obrigado por avisar! 🙏\n\n"
            + "Se quiser, escreva aqui o *motivo* (não é obrigatório).\n\n"
            + "Para marcar uma nova data, procure o *posto de saúde* onde você é atendido(a).",
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    // Texto livre só é motivo quando há estado AguardandoMotivo ativo para o telefone.
    private async Task TratarTextoLivreComoMotivoAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ctx.Texto)) return;
        // Toque em botão nunca é motivo — ex.: "Falar com atendente" (payload atendente:)
        // durante o AguardandoMotivo deve seguir para a Central de Atendimento.
        if (!string.IsNullOrEmpty(ctx.BotaoPayload) || !string.IsNullOrEmpty(ctx.InterativoReplyId)) return;

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
        db.AgendamentoConfirmacaoEstados.Remove(estado);
        var solicitacaoId = estado.ComunicacaoPaciente?.SolicitacaoId;
        if (solicitacaoId is null) return;

        var s = await db.Solicitacoes.FirstOrDefaultAsync(
            x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct);
        if (s is null) return;

        // Estado aberto pela versão anterior (pedia o motivo ANTES de cancelar): o motivo chegou,
        // então o cancelamento vale agora.
        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Pendente)
        {
            s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
            s.ConfirmacaoCanceladaEm = DateTime.UtcNow;
            s.ConfirmadoCanal = "whatsapp-quickreply";
        }
        // Só completa o cancelamento deste fluxo que ainda está sem motivo.
        else if (s.StatusConfirmacao != StatusConfirmacaoAgendamento.Cancelada
            || s.MotivoCancelamentoPaciente is not null) return;

        var motivo = ctx.Texto.Trim();
        s.MotivoCancelamentoPaciente = motivo.Length <= 500 ? motivo : motivo[..500];
        s.AtualizadoEm = DateTime.UtcNow;

        await whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical,
            "Obrigado por explicar! 🙏 Anotamos o motivo.",
            pacienteId: ctx.PacienteId, ct: ct, origem: OrigemEnvioWhatsApp.Resposta);
    }

    private Task ResponderJaRegistradaAsync(ManipuladorContexto ctx, Solicitacao s, CancellationToken ct)
    {
        var texto = s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada
            ? "Já registramos que você *não vai* comparecer. Para marcar uma nova data, procure o "
              + "*posto de saúde* onde você é atendido(a)."
            : $"Sua presença já está *confirmada* ✅. Se precisar mudar, procure o *posto de saúde* "
              + $"onde você é atendido(a).\n\n{LembreteGuia}";
        return whatsApp.EnviarTextoAsync(ctx.Conversa.TelefoneCanonical, texto, pacienteId: ctx.PacienteId, ct: ct,
            origem: OrigemEnvioWhatsApp.Resposta);
    }

    private Task<Solicitacao?> CarregarAsync(Guid solicitacaoId, CancellationToken ct) =>
        db.Solicitacoes
            .Include(x => x.ExameImagem!).ThenInclude(e => e.TipoExame)
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct);

    /// <summary>"no exame de X do dia dd/MM/aaaa às HH:mm" / "na consulta de Y…" — exame e consulta
    /// escritos certo, com a data, para a pessoa saber de QUAL agendamento se trata.</summary>
    internal static string DescricaoAgendamento(Solicitacao s, bool cancelamento = false)
    {
        var consulta = s.Categoria == CategoriaSolicitacao.Consulta;
        var nome = s.ExameImagem?.TipoExame?.Nome ?? s.EspecialidadeTexto ?? s.ProcedimentoTexto;
        var artigo = cancelamento
            ? (consulta ? "à consulta" : "ao exame")
            : (consulta ? "na consulta" : "no exame");
        var texto = string.IsNullOrWhiteSpace(nome) ? artigo : $"{artigo} de *{nome.Trim()}*";
        if (s.DataAgendada is { } da)
        {
            var local = FusoBrasilia.ParaExibicao(da);
            texto += $" do dia *{local.ToString("dd/MM/yyyy", PtBr)} às {local.ToString("HH:mm", PtBr)}*";
        }
        return texto;
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
