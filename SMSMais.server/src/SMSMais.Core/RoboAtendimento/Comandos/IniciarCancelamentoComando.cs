using System.Text.Json;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Registra que o paciente NÃO vai comparecer (intenção — a equipe decide o cancelamento real).
/// Ação sensível → GATE FORTE de identidade: 4 primeiros dígitos do CPF + mês e ano de nascimento.
/// Acha o agendamento do SISREG do paciente, confere a identidade e só então marca.
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

        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        if (!GateIdentidade.Cpf4Confere(p.Cpf, cpf) || !GateIdentidade.NascimentoMesAnoConfere(p.DataNascimento, mes, ano))
            return new(false,
                "Identidade não confere. NÃO cancele. Peça novamente os 4 primeiros dígitos do CPF e o mês e ano de "
                + "nascimento; se ainda não bater, encaminhe ao atendente humano.");

        var s = await SolicitacaoRoboHelper.AcharAgendamentoFuturoAsync(db, pacienteId, ct);
        if (s is null)
            return new(false, "Não encontrei um agendamento futuro em seu nome. Oriente a procurar o posto de saúde.");
        if (s.StatusConfirmacao == StatusConfirmacaoAgendamento.Cancelada)
            return new(true, "Este agendamento já consta como cancelado. Oriente a retornar ao posto para nova marcação.");

        s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
        s.ConfirmacaoCanceladaEm = DateTime.UtcNow;
        s.ConfirmadoCanal = "whatsapp-robo";
        s.MotivoCancelamentoPaciente = Motivo(ctx.Args);
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new(true,
            $"Registrei que você não vai comparecer ({SolicitacaoRoboHelper.Procedimento(s)}). Oriente a retornar ao "
            + "posto de saúde para uma nova marcação em data futura.");
    }

    private static string Motivo(JsonElement args)
    {
        var m = GateIdentidade.LerString(args, "motivo");
        if (string.IsNullOrWhiteSpace(m)) return "Cancelamento informado pelo robô";
        m = m.Trim();
        return m.Length <= 500 ? m : m[..500];
    }
}
