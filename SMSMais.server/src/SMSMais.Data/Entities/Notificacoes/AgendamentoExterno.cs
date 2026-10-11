using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Notificacoes;

/// <summary>Em que pé está o agendamento externo, do ponto de vista do aviso ao paciente.</summary>
public enum SituacaoAgendamentoExterno
{
    /// <summary>É o agendamento que o sistema de destino mostra hoje.</summary>
    Ativo = 1,

    /// <summary>O sistema de destino passou a mostrar OUTRA data: esta linha ficou de histórico e
    /// uma nova nasceu com a data nova (o paciente é avisado de novo).</summary>
    Reagendado = 2,

    /// <summary>O pedido saiu de "Agendada" no sistema de destino (cancelado, voltou à fila, saiu
    /// da fila). O que ainda não tinha saído para o paciente não sai mais.</summary>
    Desmarcado = 3,
}

/// <summary>
/// Um agendamento do SER, do SERNIT ou do ESUS de São Gonçalo, visto pelo lado do AVISO ao paciente
/// (10/10/2026). É a âncora da <see cref="ComunicacaoPaciente"/> desses sistemas, no lugar da
/// <see cref="Solicitacao"/>, que só existe para o SISREG.
///
/// <para><b>Por que não promover o pedido à <see cref="Solicitacao"/></b> (o que o ADR-0042 deixava
/// em aberto): a espinha exige unidade EXECUTANTE do município e alimenta worklist, recepção, gate do
/// PACS e indicadores — um atendimento no Rio de Janeiro não pertence a nenhum deles. O aviso só
/// precisa do que o paciente tem de saber: o quê, quando, onde, e a resposta dele.</para>
///
/// <para>Uma linha por (pedido do espelho × data): a data nova vira linha nova, e a antiga fica como
/// <see cref="SituacaoAgendamentoExterno.Reagendado"/> — é o que deixa o paciente ser avisado da
/// remarcação sem perder o histórico do primeiro aviso. A resposta do paciente (confirmou / não vai)
/// mora AQUI e não volta ao sistema de destino: nada é escrito no SER, no SERNIT ou no ESUS.</para>
/// </summary>
public class AgendamentoExterno
{
    public Guid Id { get; set; }

    /// <summary>Ser, Sernit ou EsusSg.</summary>
    public SistemaRegulacao Sistema { get; set; }

    /// <summary>Id da linha do espelho (<c>ser_solicitacao</c>, <c>sernit_solicitacao</c> ou
    /// <c>esussg_solicitacao</c>) — sem FK, porque são três tabelas.</summary>
    public Guid OrigemId { get; set; }

    /// <summary>O número do pedido no sistema de destino, como a equipe o procura.</summary>
    public string NumeroExterno { get; set; } = string.Empty;

    /// <summary>Aponta para fhir.patient (hub FHIR) — sem FK/navegação local.</summary>
    public Guid PacienteId { get; set; }

    public TipoAgendamento Tipo { get; set; } = TipoAgendamento.Consulta;

    /// <summary>O procedimento como o sistema de destino mostra ("CONSULTA EM ODONTOLOGIA - ...").</summary>
    public string Procedimento { get; set; } = string.Empty;

    /// <summary>Dia do atendimento (Brasília).</summary>
    public DateOnly Data { get; set; }

    /// <summary>Hora do atendimento (Brasília). Nula quando o sistema de destino não informou.</summary>
    public TimeOnly? Hora { get; set; }

    /// <summary>Onde, como o sistema de destino mostra. O endereço vem do cadastro de Unidades de
    /// Atendimento, pelo nome ligado por uma pessoa (<see cref="UnidadeAtendimentoNomeExterno"/>).</summary>
    public string? LocalNome { get; set; }

    public SituacaoAgendamentoExterno Situacao { get; set; } = SituacaoAgendamentoExterno.Ativo;
    public DateTime? EncerradoEm { get; set; }

    // ---- Resposta do PACIENTE (mesma semântica da Solicitacao) ----

    public StatusConfirmacaoAgendamento StatusConfirmacao { get; set; } = StatusConfirmacaoAgendamento.Pendente;
    public DateTime? ConfirmadoEm { get; set; }

    /// <summary>"whatsapp-quickreply" (é o único canal por enquanto).</summary>
    public string? ConfirmadoCanal { get; set; }
    public DateTime? ConfirmacaoCanceladaEm { get; set; }
    public string? MotivoCancelamentoPaciente { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    /// <summary>Concorrência otimista (PG xmin): a varredura e a resposta do paciente mexem na mesma linha.</summary>
    public uint RowVersion { get; set; }
}
