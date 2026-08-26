using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Telefones;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Verificação cadastral: valida os 4 primeiros dígitos do CPF (resposta ao desafio
/// <c>validacao_cadastro</c>). Confere → marca o telefone da conversa como VERIFICADO para o
/// paciente e LIBERA a confirmação real (as comunicações retidas em AguardandoVerificacaoCadastral
/// voltam para a fila; o worker reenvia, agora verificado → confirmacao_regulacao com os dados).
/// </summary>
public sealed class VerificarCadastroComando(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    ITelefoneValidacaoService telefones) : IRoboComando
{
    public ComandoRobo Comando => ComandoRobo.VerificarCadastro;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:verificar_cadastro";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        if (ctx.PacienteId is not { } pacienteId)
            return new(false, "Não identifiquei o paciente; encaminhe ao atendente humano.");

        var cpf = GateIdentidade.LerString(ctx.Args, "cpf");
        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        if (!GateIdentidade.Cpf4Confere(p.Cpf, cpf))
            return new(false,
                "Os 4 primeiros dígitos do CPF não conferem. Peça novamente, com calma; se persistir, encaminhe ao atendente humano.");

        // Marca o número da conversa como verificado para este paciente (também corrige o cadastro).
        if (!string.IsNullOrWhiteSpace(p.Cpf))
            await telefones.MarcarValidadoAsync(p.Cpf!, ctx.TelefoneCanonical, "robo-cadastral", null, ct);

        // Libera a confirmação real retida.
        var retidas = await db.ComunicacoesPaciente
            .Where(n => n.PacienteId == pacienteId
                && n.Status == StatusComunicacao.AguardandoVerificacaoCadastral
                && n.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento)
            .ToListAsync(ct);
        var agora = DateTime.UtcNow;
        foreach (var n in retidas)
        {
            n.Status = StatusComunicacao.Pendente;
            n.ProximaTentativaEm = agora;
            n.MotivoFalha = null;
        }
        if (retidas.Count > 0) await db.SaveChangesAsync(ct);

        return new(true,
            "Identidade confirmada. Diga que agora você vai enviar as informações do agendamento em seguida "
            + "(a confirmação chega logo). Seja breve e cordial.");
    }
}
