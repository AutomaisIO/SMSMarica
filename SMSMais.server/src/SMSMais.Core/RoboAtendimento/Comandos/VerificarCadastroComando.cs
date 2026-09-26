using SMSMais.Core.Notificacoes.Comunicacao;
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
///
/// <para><b>Nunca TROCA um número verificado</b> (regra do produto, 17/09/2026): o robô só carimba
/// quando o paciente ainda não tem contato provado. Já havendo um, trocar exigiria os 4 dígitos do
/// CPF e mais nada — seria a porta de trás da regra que manda ir ao posto ([ADR-0057]). Nesse caso
/// o robô não carimba: apenas libera o que estava retido (que sai para o número já verificado) e
/// orienta o posto.</para>
///
/// <para>A liberação é a MESMA da máquina de verificação do WhatsApp
/// (<see cref="ILiberacaoAposIdentificacao"/>: confirmação e lembrete retidos, uma mensagem por
/// agendamento, e nada prometido que o envio vá barrar). O modelo recebe quantas saíram e, com
/// zero, o motivo — para não dizer "vou enviar" quando nada vai sair.</para>
/// </summary>
public sealed class VerificarCadastroComando(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    ITelefoneValidacaoService telefones,
    ILiberacaoAposIdentificacao liberacao) : IRoboComando
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
        if (!GateIdentidade.CpfInicioConfere(p.Cpf, cpf))
            return new(false,
                "Os primeiros dígitos do CPF não conferem. Peça novamente, com calma; se persistir, encaminhe ao atendente humano.");

        // Já existe contato provado, e é OUTRO número? Não troca — nem por aqui.
        var jaTemOutroVerificado = !string.IsNullOrWhiteSpace(p.TelefoneVerificado)
            && !TelefoneValidacaoService.EhMesmoNumero(p.TelefoneVerificado, ctx.TelefoneCanonical);
        if (jaTemOutroVerificado)
        {
            return new(false,
                "Este cadastro já tem um WhatsApp verificado, e não é este número. NÃO diga que "
                + "confirmou a identidade e NÃO prometa enviar dados por aqui. Explique que, para "
                + "trocar o número do cadastro, é preciso procurar o posto de saúde onde a pessoa é "
                + "atendida, levando um documento com foto.");
        }

        // Marca o número da conversa como verificado para este paciente (também corrige o cadastro).
        if (!string.IsNullOrWhiteSpace(p.Cpf))
            await telefones.MarcarValidadoAsync(p.Cpf!, ctx.TelefoneCanonical, "robo-cadastral", null, ct,
                pacienteId: pacienteId);

        // Libera o que estava retido (confirmação e lembrete) — o robô não tem diálogo próprio,
        // então não há "pendurada": vale o que o paciente tem esperando identificação.
        var resultado = await liberacao.LiberarAsync(pacienteId, penduradaId: null, ct);
        await db.SaveChangesAsync(ct);

        return new(true, InstrucaoAoModelo(resultado));
    }

    /// <summary>O que o modelo pode dizer — só promete envio quando algo foi para a fila.</summary>
    internal static string InstrucaoAoModelo(LiberacaoResultado r)
    {
        if (r.Liberadas > 0)
            return $"Identidade confirmada; {r.Liberadas} aviso(s) liberado(s) para envio. Diga que agora você vai "
                + "enviar as informações do agendamento em seguida (a confirmação chega logo). Seja breve e cordial.";

        var oQueDizer = r.Desfecho switch
        {
            DesfechoLiberacao.AtendenteAssumiu =>
                "Diga que uma atendente da equipe já está cuidando desse agendamento e fala com a pessoa por aqui "
                + "em horário de atendimento.",
            DesfechoLiberacao.AgendamentoCancelado =>
                "Diga que esse agendamento foi cancelado e que, se precisar remarcar, deve procurar o posto de saúde "
                + "onde o paciente é atendido.",
            DesfechoLiberacao.AgendamentoPassou =>
                "Diga que a data desse agendamento já passou e que, se precisar remarcar, deve procurar o posto de "
                + "saúde onde o paciente é atendido.",
            DesfechoLiberacao.JaEnviado =>
                "Diga que as informações desse agendamento já foram enviadas antes, na mensagem com data e local.",
            _ => "Diga que no momento não há aviso pendente para o paciente e que, quando houver novidade, chega por aqui.",
        };
        return $"Identidade confirmada, mas NENHUM aviso foi liberado ({r.Motivo}). NÃO diga que vai enviar "
            + $"informações. {oQueDizer} Seja breve e cordial.";
    }
}
