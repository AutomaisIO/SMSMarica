using System.Globalization;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Confirma a presença do paciente no agendamento futuro mais próximo. Como vem por conversa
/// (robô), tem o mesmo risco do "1 telefone, várias pessoas" → GATE de identidade: 4 primeiros
/// dígitos do CPF + mês e ano de nascimento. Confere, DEVOLVE o agendamento encontrado (para o
/// robô mostrar qual é) e marca aquele como Confirmada.
/// </summary>
public sealed class ConfirmarPresencaComando(SmsMaisDbContext db, IPacientesService pacientes) : IRoboComando
{
    public ComandoRobo Comando => ComandoRobo.ConfirmarPresenca;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:confirmar";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        if (ctx.PacienteId is not { } pacienteId)
            return new(false, "Não identifiquei o paciente desta conversa; encaminhe ao atendente humano.");

        var cpf = GateIdentidade.LerString(ctx.Args, "cpf");
        var mes = GateIdentidade.LerInt(ctx.Args, "mesNascimento");
        var ano = GateIdentidade.LerInt(ctx.Args, "anoNascimento");

        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        if (!GateIdentidade.Cpf4Confere(p.Cpf, cpf) || !GateIdentidade.NascimentoMesAnoConfere(p.DataNascimento, mes, ano))
            return new(false,
                "Identidade não confere. NÃO confirme. Peça novamente os 4 primeiros dígitos do CPF e o mês e ano de "
                + "nascimento; se ainda não bater, encaminhe ao atendente humano.");

        var s = await SolicitacaoRoboHelper.AcharAgendamentoFuturoAsync(db, pacienteId, ct);
        if (s is null)
            return new(false, "Não encontrei um agendamento futuro em seu nome para confirmar.");

        var descricao = $"{SolicitacaoRoboHelper.Procedimento(s)} em {FormatarData(s.DataAgendada)}";

        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada)
            return new(false, $"O agendamento ({descricao}) consta como cancelado; oriente a procurar o posto de saúde.");
        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada)
            return new(true, $"Sua presença em {descricao} já estava confirmada. Agradeça e finalize.");

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
        s.ConfirmadoEm = DateTime.UtcNow;
        s.ConfirmadoCanal = "whatsapp-robo";
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new(true, $"Presença CONFIRMADA em {descricao}. Mostre à pessoa qual agendamento foi confirmado e agradeça.");
    }

    // Regra única de fuso: Brasília fixo (UTC-3).
    private static string FormatarData(DateTime? dataUtc) =>
        dataUtc is { } d
            ? d.AddHours(-3).ToString("dd/MM 'às' HH'h'mm", CultureInfo.GetCultureInfo("pt-BR"))
            : "data a confirmar";
}
