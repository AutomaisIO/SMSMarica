using SMSMais.Core.Pacientes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Confere a identidade da pessoa contra o cadastro — 4 primeiros dígitos do CPF + mês/ano de
/// nascimento — e devolve o NOME para o robô tratar a pessoa corretamente e seguir com segurança.
///
/// Existe por causa de um caso real: sem nenhuma ferramenta na conversa, o robô pediu CPF e data de
/// nascimento, não consultou nada e ainda afirmou ao cidadão que a identidade "não conferia" — com
/// os dados dele corretos. Verificação é consulta, não conversa: ou existe uma ferramenta que
/// confere de verdade, ou o robô não pode nem pedir os dados.
///
/// Minimização: em caso de acerto devolve só o nome (e se o contato já é verificado). Em caso de
/// erro não diz QUAL dado não bateu — isso viraria um oráculo para adivinhar cadastro alheio.
/// </summary>
public sealed class ConsultarCadastroComando(IPacientesService pacientes) : IRoboComando
{
    public ComandoRobo Comando => ComandoRobo.ConsultarCadastro;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:consultar_cadastro";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        if (ctx.PacienteId is not { } pacienteId)
            return new(false,
                "Este número não está vinculado a um cadastro, então não há o que conferir. NÃO peça "
                + "mais dados pessoais: oriente a pessoa a procurar o posto de saúde onde é atendida.");

        var cpf = GateIdentidade.LerString(ctx.Args, "cpf");
        var mes = GateIdentidade.LerInt(ctx.Args, "mesNascimento");
        var ano = GateIdentidade.LerInt(ctx.Args, "anoNascimento");

        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        var cpfOk = GateIdentidade.CpfInicioConfere(p.Cpf, cpf);
        var nascimentoOk = GateIdentidade.NascimentoMesAnoConfere(p.DataNascimento, mes, ano);

        if (!cpfOk || !nascimentoOk)
            return new(false,
                "Os dados não conferem com o cadastro. NÃO diga qual deles falhou. Explique que o CPF "
                + "e a data de nascimento precisam ser os do PACIENTE do agendamento — se quem "
                + "escreve é parente ou responsável, os dados devem ser do paciente — e ofereça "
                + "tentar de novo.");

        var nome = (p.NomeCompleto ?? string.Empty).Trim();
        var verificado = !string.IsNullOrWhiteSpace(p.TelefoneVerificado);
        return new(true,
            $"Identidade confirmada: {nome}. Trate a pessoa pelo primeiro nome e siga com o "
            + "atendimento. NÃO repita o CPF nem a data de nascimento na resposta."
            + (verificado ? string.Empty : " O contato ainda não está verificado no cadastro."));
    }
}
