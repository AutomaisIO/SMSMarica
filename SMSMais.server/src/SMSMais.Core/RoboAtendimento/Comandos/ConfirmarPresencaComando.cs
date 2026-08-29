using System.Globalization;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Confirma a presença do paciente no agendamento futuro. GATE de identidade (4 primeiros dígitos
/// do CPF + mês e ano de nascimento) e DUAS FASES: primeiro valida e devolve o NOME COMPLETO +
/// agendamento para a pessoa confirmar; só com <c>confirmado=true</c> marca. NUNCA revela o
/// agendamento antes de a identidade conferir; agendamento passado não conta (só futuro).
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
        var confirmado = GateIdentidade.LerBool(ctx.Args, "confirmado");

        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        if (!GateIdentidade.CpfInicioConfere(p.Cpf, cpf) || !GateIdentidade.NascimentoMesAnoConfere(p.DataNascimento, mes, ano))
            return new(false,
                "Identidade não confere. NÃO confirme nem revele o agendamento. Peça novamente os 4 primeiros dígitos do "
                + "CPF e o mês e ano de nascimento; se ainda não bater, encaminhe ao atendente humano.");

        var s = await SolicitacaoRoboHelper.AcharAgendamentoFuturoAsync(db, pacienteId, ct);
        if (s is null)
            return new(true, "Não há nenhum agendamento FUTURO em nome desta pessoa. Diga que não há nada agendado para "
                + "confirmar; NÃO invente datas nem mencione agendamentos passados.");

        var nome = string.IsNullOrWhiteSpace(p.NomeCompleto) ? "(nome não encontrado)" : p.NomeCompleto!;
        var descricao = $"{SolicitacaoRoboHelper.Procedimento(s)} em {FormatarData(s.DataAgendada)}";

        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada)
            return new(false, $"O agendamento ({descricao}) consta como cancelado; oriente a procurar o posto de saúde.");

        if (!confirmado)
            return new(true,
                $"Identidade confirmada. Confirme o NOME com a pessoa antes de marcar: você é *{nome}*? O agendamento é "
                + $"{descricao}. Se ela confirmar que é essa pessoa, chame 'confirmar_presenca' de novo com confirmado=true.");

        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Confirmada)
            return new(true, $"A presença de {nome} em {descricao} já estava confirmada. Agradeça e finalize.");

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Confirmada;
        s.ConfirmadoEm = DateTime.UtcNow;
        s.ConfirmadoCanal = "whatsapp-robo";
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new(true, $"Presença CONFIRMADA para {nome} ({descricao}). Agradeça pela confirmação.");
    }

    // Regra única de fuso: Brasília fixo (UTC-3).
    private static string FormatarData(DateTime? dataUtc) =>
        dataUtc is { } d
            ? d.AddHours(-3).ToString("dd/MM 'às' HH'h'mm", CultureInfo.GetCultureInfo("pt-BR"))
            : "data a confirmar";
}
