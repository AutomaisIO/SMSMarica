using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.RoboAtendimento.Comandos;

/// <summary>
/// Consulta a POSIÇÃO de um agendamento na regulação (espelhos SER/Estado e SERNIT/Niterói),
/// localizando por CPF. Devolve dado MINIMIZADO: em regra só "em fila"; se cancelada, sinaliza
/// que um atendente vai contatar. NUNCA expõe dados internos (datas, unidade, procedimento,
/// solicitante, prioridade).
/// </summary>
public sealed class ConsultarPosicaoRegulacaoComando(SmsMaisDbContext db, IPacientesService pacientes) : IRoboComando
{
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
                    "Para consultar a regulação preciso confirmar a identidade: peça os 3 primeiros dígitos do CPF e o "
                    + "mês e ano de nascimento e chame novamente. Se não conferir, encaminhe ao atendente humano.");
        }

        if (string.IsNullOrWhiteSpace(p.Cpf))
            return new(false, "Sem CPF no cadastro para localizar na regulação; oriente a procurar o posto de saúde.");

        var cancelada =
            await db.SerSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf && s.Situacao == SituacaoSer.Cancelada, ct)
            || await db.SernitSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf && s.Situacao == SituacaoSernit.Cancelada, ct);

        var existe = cancelada
            || await db.SerSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf, ct)
            || await db.SernitSolicitacoes.AsNoTracking().AnyAsync(s => s.Cpf == p.Cpf, ct);

        if (!existe)
            return new(false,
                "Não localizei sua solicitação na regulação. Oriente a procurar o posto de saúde onde é cadastrado.");

        if (cancelada)
            return new(true,
                "Diga que a solicitação está em fila e que você vai pedir para um ATENDENTE entrar em contato para tratar "
                + "do assunto; encerre com handoff=true. NÃO revele datas, unidade nem qualquer dado interno.");

        return new(true,
            "Diga apenas que a solicitação está EM FILA aguardando regulação. NÃO revele datas, unidade, procedimento "
            + "nem qualquer dado interno.");
    }
}
