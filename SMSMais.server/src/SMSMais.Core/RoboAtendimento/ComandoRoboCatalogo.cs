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
        new(ComandoRobo.ConsultarStatusAgendamento, "Consultar status do agendamento",
            "Consulta (só leitura) a situação do agendamento/solicitação do paciente.", false),
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
    ];

    /// <summary>Comandos que a tela pode habilitar por assunto (exclui ResponderCidadao).</summary>
    public static IReadOnlySet<ComandoRobo> Habilitaveis { get; } =
        Itens.Select(i => i.Comando).ToHashSet();
}
