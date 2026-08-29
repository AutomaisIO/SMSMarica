using System.Globalization;
using System.Text.Json;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Registra que o paciente NÃO vai comparecer (intenção — a equipe decide o cancelamento real).
/// GATE de identidade (4 primeiros dígitos do CPF + mês e ano de nascimento) e DUAS FASES: primeiro
/// valida e devolve o NOME COMPLETO + agendamento para a pessoa confirmar; só com <c>confirmado=true</c>
/// marca. NUNCA revela o agendamento antes da identidade conferir; só agendamentos futuros contam.
/// </summary>
public sealed class IniciarCancelamentoComando(SmsMaisDbContext db, IPacientesService pacientes) : IRoboComando
{
    public ComandoRobo Comando => ComandoRobo.IniciarCancelamento;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:cancelar";

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
                "Identidade não confere. NÃO cancele nem revele o agendamento. Peça novamente os 4 primeiros dígitos do "
                + "CPF e o mês e ano de nascimento; se ainda não bater, encaminhe ao atendente humano.");

        var s = await SolicitacaoRoboHelper.AcharAgendamentoFuturoAsync(db, pacienteId, ct);
        if (s is null)
            return new(true, "Não há nenhum agendamento FUTURO em nome desta pessoa; não há o que cancelar. Oriente a "
                + "procurar o posto de saúde. NÃO invente datas nem mencione agendamentos passados.");

        var nome = string.IsNullOrWhiteSpace(p.NomeCompleto) ? "(nome não encontrado)" : p.NomeCompleto!;
        var descricao = $"{SolicitacaoRoboHelper.Procedimento(s)} em {FormatarData(s.DataAgendada)}";

        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada)
            return new(true, $"O agendamento de {nome} ({descricao}) já consta como cancelado. Oriente a retornar ao posto para nova marcação.");

        if (!confirmado)
            return new(true,
                $"Identidade confirmada. Confirme o NOME antes de registrar: você é *{nome}* e NÃO vai comparecer ao "
                + $"agendamento de {descricao}? Se ela confirmar, chame 'iniciar_cancelamento' de novo com confirmado=true.");

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
        s.ConfirmacaoCanceladaEm = DateTime.UtcNow;
        s.ConfirmadoCanal = "whatsapp-robo";
        s.MotivoCancelamentoPaciente = Motivo(ctx.Args);
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new(true,
            $"Registrei que {nome} não vai comparecer ({descricao}). Oriente a retornar ao posto de saúde para uma nova "
            + "marcação em data futura.");
    }

    private static string Motivo(JsonElement args)
    {
        var m = GateIdentidade.LerString(args, "motivo");
        if (string.IsNullOrWhiteSpace(m)) return "Cancelamento informado pelo robô";
        m = m.Trim();
        return m.Length <= 500 ? m : m[..500];
    }

    private static string FormatarData(DateTime? dataUtc) =>
        dataUtc is { } d
            ? d.AddHours(-3).ToString("dd/MM 'às' HH'h'mm", CultureInfo.GetCultureInfo("pt-BR"))
            : "data a confirmar";
}
