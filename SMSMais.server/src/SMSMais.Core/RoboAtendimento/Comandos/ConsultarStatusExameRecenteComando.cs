using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Consulta a situação do exame/laudo recente do paciente. Gate de identidade (4 primeiros dígitos
/// do CPF) — sem conferir, não revela nada. Dado MINIMIZADO: só a situação (pronto / em elaboração /
/// não realizado), nunca conteúdo clínico nem prazos.
/// </summary>
public sealed class ConsultarStatusExameRecenteComando(SmsMaisDbContext db, IPacientesService pacientes) : IRoboComando
{
    public ComandoRobo Comando => ComandoRobo.ConsultarStatusExameRecente;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:consultar_exame";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        if (ctx.PacienteId is not { } pacienteId)
            return new(false, "Não identifiquei o paciente; encaminhe ao atendente humano.");

        var cpf = GateIdentidade.LerString(ctx.Args, "cpf");
        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        if (!GateIdentidade.Cpf4Confere(p.Cpf, cpf))
            return new(false,
                "Os 4 primeiros dígitos do CPF não conferem. NÃO revele nada; peça novamente ou encaminhe ao atendente humano.");

        var status = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.PacienteId == pacienteId && s.ExcluidoEm == null && s.ExameImagem != null)
            .OrderByDescending(s => s.DataAgendada)
            .Select(s => (StatusSolicitacaoExame?)s.ExameImagem!.Status)
            .FirstOrDefaultAsync(ct);

        if (status is null)
            return new(false, "Não encontrei um exame recente em seu nome. Oriente a procurar o posto de saúde.");

        var msg = status switch
        {
            StatusSolicitacaoExame.Laudada =>
                "O laudo do seu exame está PRONTO — acompanhe no app; se ainda não recebeu, chega em breve.",
            StatusSolicitacaoExame.Realizada =>
                "Seu exame foi realizado; o laudo é escrito pelo médico e enviado assim que ficar pronto. Você será avisado.",
            _ =>
                "Seu exame ainda não consta como realizado. Quando for feito, o laudo é enviado ao ficar pronto.",
        };
        return new(true, msg + " NÃO dê prazos nem detalhes clínicos.");
    }
}
