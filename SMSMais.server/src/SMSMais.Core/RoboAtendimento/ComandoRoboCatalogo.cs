using SMSMais.Core.RoboAtendimento.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento;

/// <summary>
/// Catálogo FIXO dos comandos que a tela pode ligar/desligar por assunto. É código, não dado:
/// a tela só habilita/desabilita — comandos novos entram aqui e no <see cref="ComandoRobo"/>.
/// <see cref="ComandoRobo.ResponderCidadao"/> NÃO entra: é a ferramenta terminal obrigatória,
/// sempre presente na sessão, não um comando opcional.
/// </summary>
public static class ComandoRoboCatalogo
{
    public static IReadOnlyList<ComandoRoboCatalogoDto> Itens { get; } =
    [
        // ConsultarStatusAgendamento (enum 1) ficou fora um tempo por ser tool fantasma (estava no
        // catálogo sem handler nenhum). Voltou com handler de verdade — foi a falta dela que fez o
        // robô afirmar "não há agendamento" sem consultar nada.
        new(ComandoRobo.ConsultarStatusAgendamento, "Consultar agendamentos do paciente",
            "Lista os agendamentos futuros (procedimento, data e unidade) após conferir a identidade.", false),
        new(ComandoRobo.ConfirmarPresenca, "Confirmar presença",
            "Marca a presença confirmada pelo paciente.", true),
        new(ComandoRobo.IniciarCancelamento, "Iniciar cancelamento",
            "Registra a intenção do paciente de não comparecer (a equipe decide o cancelamento).", true),
        new(ComandoRobo.RegistrarNumeroErrado, "Registrar número errado",
            "Registra que o número não pertence ao paciente (\"não sou essa pessoa\").", true),
        new(ComandoRobo.EncaminharParaHumano, "Encaminhar para atendente",
            "Devolve a conversa para atendimento humano (hand-off).", false),
        new(ComandoRobo.InformarHorarioAtendimento, "Informar horário de atendimento",
            "Informa ao paciente o horário de atendimento da unidade.", false),
        new(ComandoRobo.ConsultarStatusExameRecente, "Consultar exame/laudo recente",
            "Consulta a situação do exame/laudo recente. Só libera após confirmar nome + 4 primeiros dígitos do CPF.",
            false),
        new(ComandoRobo.ConsultarPosicaoRegulacao, "Consultar posição na regulação",
            "Situação de agendamento na regulação (SER/SISREG/SERNIT). Devolve dado minimizado (em regra só \"em fila\").",
            false),
        new(ComandoRobo.ConsultarCadastro, "Conferir identidade no cadastro",
            "Confere 4 primeiros dígitos do CPF + mês/ano de nascimento e devolve o nome. Sem este comando, o robô não pode pedir dado pessoal.",
            false),
        new(ComandoRobo.ConsultarUnidades, "Consultar unidades (nome e endereço)",
            "Diz onde fica um posto, sem inventar endereço. Não é oferta de atendimento.", false),
        // Tinha handler e ferramenta, mas estava FORA do catálogo: a tela recusava habilitá-lo
        // (RoboAssuntoService valida contra Habilitaveis) e só funcionava semeado no banco.
        new(ComandoRobo.VerificarCadastro, "Verificar cadastro (desafio do CPF)",
            "Valida os primeiros dígitos do CPF em resposta ao desafio cadastral e libera a confirmação retida.",
            true),
    ];

    /// <summary>
    /// Comandos disponíveis SEMPRE, mesmo sem assunto identificado — e 22% das mensagens caem sem
    /// assunto. Sem isto o robô fica sem ferramenta nenhuma justamente no caso mais comum: foi
    /// assim que ele pediu CPF e data de nascimento, não pôde consultar nada e ainda afirmou que a
    /// identidade não conferia. Proibi-lo de pedir não resolveu — ele copiava o próprio histórico.
    /// Dar a ferramenta resolve.
    ///
    /// Só entram comandos SEGUROS — leitura, e nada que altere dado (isso continua por assunto):
    /// conferir identidade e ver agendamento (ambos com minimização e atrás do gate de CPF+data),
    /// dizer onde fica uma unidade (endereço é informação pública) e devolver a conversa a um
    /// humano. Os dois de consulta estão aqui pelo mesmo motivo: sem eles o robô AFIRMA no lugar de
    /// consultar — foi assim que desmentiu um agendamento que a própria Secretaria havia enviado.
    /// </summary>
    public static IReadOnlySet<ComandoRobo> Base { get; } = new HashSet<ComandoRobo>
    {
        ComandoRobo.ConsultarCadastro,
        ComandoRobo.ConsultarStatusAgendamento,
        ComandoRobo.ConsultarUnidades,
        ComandoRobo.EncaminharParaHumano,
    };

    /// <summary>Comandos que a tela pode habilitar por assunto (exclui ResponderCidadao).</summary>
    public static IReadOnlySet<ComandoRobo> Habilitaveis { get; } =
        Itens.Select(i => i.Comando).ToHashSet();
}
