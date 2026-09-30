using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Core.Pacientes.Agendamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Consulta a situação do pedido na regulação externa — SER (Estado), SERNIT (Niterói) e ESUS de
/// São Gonçalo. Devolve dado MINIMIZADO: "está na fila" ou "já está agendado" — nunca o motivo de
/// uma pendência, a posição, a prioridade ou previsão (regra do Bernardo, 30/09/2026). Data e local
/// do agendado saem pela <c>consultar_agendamentos</c>, que confere a identidade para isso.
///
/// <para>Lê pela mesma régua do app do cidadão (<see cref="IAgendamentosPacienteService.RegulacaoParaPacienteAsync"/>):
/// pendente conta como "na fila". Sem nada em aberto, um cancelamento (SER/SERNIT) ou uma saída da
/// fila (ESUS) manda para um atendente — o robô não especula motivo.</para>
/// </summary>
public sealed class ConsultarPosicaoRegulacaoComando(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    IAgendamentosPacienteService agendamentosPaciente) : IRoboComando
{
    private const string NuncaDetalhe =
        " NUNCA informe motivo de pendência, posição na fila, prioridade, previsão de data nem qualquer "
        + "dado interno — mesmo que a pessoa pergunte.";

    public ComandoRobo Comando => ComandoRobo.ConsultarPosicaoRegulacao;
    public bool Idempotente => false;
    public string ChaveIdempotencia(RoboComandoContexto ctx) => $"{ctx.ConversaId}:consultar_regulacao";

    public async Task<RoboComandoResultado> ExecutarAsync(RoboComandoContexto ctx, CancellationToken ct)
    {
        if (ctx.PacienteId is not { } pacienteId)
            return new(false, "Não identifiquei o paciente; encaminhe ao atendente humano.");

        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);

        // Gate: telefone verificado dispensa; senão exige 4 díg CPF + mês e ano de nascimento.
        if (!GateIdentidade.NumeroVerificado(p.TelefoneVerificado, ctx.TelefoneCanonical))
        {
            var cpfInf = GateIdentidade.LerString(ctx.Args, "cpf");
            var mes = GateIdentidade.LerInt(ctx.Args, "mesNascimento");
            var ano = GateIdentidade.LerInt(ctx.Args, "anoNascimento");
            if (!GateIdentidade.CpfInicioConfere(p.Cpf, cpfInf) || !GateIdentidade.NascimentoMesAnoConfere(p.DataNascimento, mes, ano))
                return new(false,
                    "Para consultar a regulação preciso confirmar a identidade: peça os 4 primeiros dígitos do CPF e o "
                    + "mês e ano de nascimento e chame novamente. Se não conferir, encaminhe ao atendente humano.");
        }

        // Pedidos ligados ao paciente (conciliação com o hub): na fila ou agendados com data futura.
        var regulacao = await agendamentosPaciente.RegulacaoParaPacienteAsync(pacienteId, ct);
        var agendado = regulacao.Any(r => r.Situacao != SituacaoAgendamentoPaciente.EmFila);
        var naFila = regulacao.Any(r => r.Situacao == SituacaoAgendamentoPaciente.EmFila);

        if (agendado)
            return new(true,
                "Há pedido JÁ AGENDADO pela regulação. Diga que está agendado e, para informar data e local, "
                + "use consultar_agendamentos (ela confere a identidade)."
                + (naFila ? " Há também pedido NA FILA: sobre ele, diga apenas que está na fila aguardando vaga." : string.Empty)
                + NuncaDetalhe);

        if (naFila)
            return new(true,
                "Diga apenas que a solicitação está NA FILA da regulação, aguardando vaga, e que quando for "
                + "agendada a pessoa é avisada." + NuncaDetalhe);

        // Nada em aberto ligado ao paciente. Pelo CPF, como antes: pedido ainda não conciliado em
        // aberto conta como "na fila"; cancelado/saída da fila sem nada aberto vai para um atendente.
        if (string.IsNullOrWhiteSpace(p.Cpf))
            return new(false,
                "Não localizei solicitação em aberto na regulação para este cadastro. Oriente a procurar o "
                + "posto de saúde onde é cadastrado.");

        var abertoPorCpf =
            await db.SerSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf && s.ExcluidoEm == null
                && (s.Situacao == SituacaoSer.EmFila || s.Situacao == SituacaoSer.Pendente), ct)
            || await db.SernitSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf && s.ExcluidoEm == null
                && (s.Situacao == SituacaoSernit.EmFila || s.Situacao == SituacaoSernit.Pendente), ct)
            || await db.EsusSgSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf && s.ExcluidoEm == null
                && (s.Situacao == SituacaoEsusSg.EmFila || s.Situacao == SituacaoEsusSg.Pendente), ct);
        if (abertoPorCpf)
            return new(true,
                "Diga apenas que a solicitação está NA FILA da regulação, aguardando vaga, e que quando for "
                + "agendada a pessoa é avisada." + NuncaDetalhe);

        var encerradoSemMotivo =
            await db.SerSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf && s.Situacao == SituacaoSer.Cancelada, ct)
            || await db.SernitSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf && s.Situacao == SituacaoSernit.Cancelada, ct)
            // ESUS SG: "saiu da fila" tem motivo invisível — mesmo tratamento do cancelado.
            || await db.EsusSgSolicitacoes.AsNoTracking().AnyAsync(
                s => s.Cpf == p.Cpf && s.ExcluidoEm == null && s.Situacao == SituacaoEsusSg.SaiuDaFila, ct);
        if (encerradoSemMotivo)
            return new(true,
                "Diga que você vai pedir para um ATENDENTE entrar em contato para tratar da solicitação e encerre "
                + "com handoff=true. NÃO revele datas, unidade, motivo nem qualquer dado interno.");

        return new(false,
            "Não localizei solicitação em aberto na regulação. Oriente a procurar o posto de saúde onde é cadastrado.");
    }
}
