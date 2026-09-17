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
public sealed class ConsultarCadastroComando(
    IPacientesService pacientes,
    PendenciasCadastro.IContatoNegadoService contatosNegados) : IRoboComando
{
    public ComandoRobo Comando => ComandoRobo.ConsultarCadastro;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:consultar_cadastro";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        var cpf = GateIdentidade.LerString(ctx.Args, "cpf");
        var mes = GateIdentidade.LerInt(ctx.Args, "mesNascimento");
        var ano = GateIdentidade.LerInt(ctx.Args, "anoNascimento");

        // DADO FALTANDO ≠ DADO ERRADO. O modelo já chegou a chamar isto com "<UNKNOWN>" no mês/ano
        // em vez de perguntar — e o cidadão recebeu "não localizei seu cadastro", o que é mentira.
        // Aqui a diferença é explícita: falta é pedido, não veredito.
        if (string.IsNullOrWhiteSpace(cpf) || GateIdentidade.SoDigitos(cpf).Length < 4)
            return new(false,
                "Faltam os dígitos do CPF. NÃO conclua nada: peça à pessoa os *4 primeiros dígitos "
                + "do CPF* do paciente, todos de uma vez, e chame de novo.");
        if (mes is null || ano is null)
            return new(false,
                "Falta a data de nascimento. NÃO conclua nada e NÃO diga que não encontrou o "
                + "cadastro: peça o MÊS e o ANO de nascimento do paciente e chame de novo.");

        // O paciente vem da conversa; quando o número não está amarrado (caso comum), procura no
        // cadastro por telefone — vários casos reais chegam por número que a conversa ainda não
        // resolveu, e sem isso a verificação seria impossível justamente para eles.
        var candidatos = new List<(Guid Id, string Nome, string? Cpf, DateOnly? Nascimento, string? Verificado)>();
        if (ctx.PacienteId is { } pacienteId)
        {
            var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
            candidatos.Add((p.Id, p.NomeCompleto, p.Cpf, p.DataNascimento, p.TelefoneVerificado));
        }
        else
        {
            foreach (var p in await pacientes.ListarPorTelefoneAsync(ctx.TelefoneCanonical, ct))
            {
                // LGPD: paciente cujo contato foi NEGADO neste número não entra na conferência.
                if (await contatosNegados.BloqueadoAsync(ctx.TelefoneCanonical, p.Id, ct)) continue;
                candidatos.Add((p.Id, p.NomeCompleto, p.Cpf, p.DataNascimento, null));
            }
        }

        if (candidatos.Count == 0)
            return new(false,
                "Este número não está vinculado a nenhum cadastro nosso — não é que os dados estejam "
                + "errados, é que não há a quem comparar. Diga isso com clareza, NÃO peça mais dados "
                + "pessoais e oriente a procurar o posto de saúde onde a pessoa é atendida.");

        var achado = candidatos.FirstOrDefault(c =>
            GateIdentidade.CpfInicioConfere(c.Cpf, cpf)
            && GateIdentidade.NascimentoMesAnoConfere(c.Nascimento, mes, ano));

        if (achado.Id == Guid.Empty)
            return new(false,
                "Os dados não conferem com o cadastro. NÃO diga qual deles falhou. Explique que o CPF "
                + "e a data de nascimento precisam ser os do PACIENTE do agendamento — se quem "
                + "escreve é parente ou responsável, os dados devem ser do paciente — e ofereça "
                + "tentar de novo.");

        var verificado = !string.IsNullOrWhiteSpace(achado.Verificado);
        return new(true,
            $"Identidade confirmada: {(achado.Nome ?? string.Empty).Trim()}. Trate a pessoa pelo "
            + "primeiro nome e siga com o atendimento. NÃO repita o CPF nem a data de nascimento na "
            + "resposta." + (verificado ? string.Empty : " O contato ainda não está verificado no cadastro."),
            new { pacienteId = achado.Id });
    }
}
