namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Catálogo fixo dos comandos que o robô de atendimento pode executar. Cada valor é mapeado
/// a um handler .NET (RoboComandoRegistry) e exposto como uma ferramenta MCP no motor de IA;
/// a tela de assuntos apenas LIGA/DESLIGA cada comando por assunto (robo_assunto_comando).
/// O valor inteiro é estável (persistido) — não renumerar; comandos novos entram no fim.
/// </summary>
public enum ComandoRobo
{
    /// <summary>Só leitura: consulta a situação do agendamento/solicitação do paciente.</summary>
    ConsultarStatusAgendamento = 1,

    /// <summary>Marca a presença confirmada (guard: no-op se a confirmação já não está Pendente).</summary>
    ConfirmarPresenca = 2,

    /// <summary>Inicia o cancelamento pelo paciente (intenção — a equipe decide o cancelamento real).</summary>
    IniciarCancelamento = 3,

    /// <summary>Registra que o número não pertence ao paciente ("não sou essa pessoa").</summary>
    RegistrarNumeroErrado = 4,

    /// <summary>Encaminha a conversa para atendimento humano (hand-off).</summary>
    EncaminharParaHumano = 5,

    /// <summary>Informa o horário de atendimento da unidade.</summary>
    InformarHorarioAtendimento = 6,

    /// <summary>Ferramenta terminal obrigatória: entrega a resposta final ao cidadão (texto +
    /// decisão de hand-off + confiança). Sempre habilitada, não é opcional por assunto.</summary>
    ResponderCidadao = 7,

    /// <summary>Consulta a situação do EXAME/LAUDO recente do paciente. Só libera o dado APÓS
    /// verificação de identidade: nome confere + os 4 primeiros dígitos do CPF batem (o handler
    /// aceita o CPF inteiro e usa os 4 primeiros). Sem conferir, não revela nada.</summary>
    ConsultarStatusExameRecente = 8,

    /// <summary>Consulta a POSIÇÃO de um agendamento na regulação (SER/SISREG/SERNIT). O handler
    /// devolve dado MINIMIZADO: em regra só "em fila"; tentativas de contato (followup) quando
    /// houver; nada quando houver pendência; e sinaliza cancelamento (bot pede atendente). Nunca
    /// expõe dados internos (datas, códigos, unidade, procedimento, profissional, prioridade).</summary>
    ConsultarPosicaoRegulacao = 9,

    /// <summary>Verificação cadastral: valida os 4 primeiros dígitos do CPF (resposta ao template
    /// <c>validacao_cadastro</c>). Confere → marca o telefone verificado e libera o envio da
    /// confirmação real do agendamento. Aceita CPF inteiro (usa os 4 primeiros).</summary>
    VerificarCadastro = 10,
}
