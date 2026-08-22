namespace SMSMarica.Core.Pacientes.Agendamentos.Dtos;

/// <summary>Sistema que regulou/registrou o agendamento.</summary>
public enum OrigemAgendamentoPaciente
{
    /// <summary>Regulação estadual (SER — SES-RJ), espelho <c>ser_solicitacao</c>.</summary>
    Ser = 1,

    /// <summary>Regulação municipal/SISREG, entidade <c>solicitacao</c>.</summary>
    Sisreg = 2,

    /// <summary>Agenda própria do município (entidade <c>agendamento</c>).</summary>
    Local = 3,
}

/// <summary>
/// Situação NORMALIZADA entre as três fontes. Cada fonte usa um vocabulário próprio
/// (SER: EmFila..Alta; SISREG: Solicitada..Cancelada; agenda local: Agendado..Faltou); aqui
/// elas são reduzidas a um conjunto único para a aba do paciente.
///
/// <para><b>Limitação de dado conhecida:</b> "compareceu/faltou" só existe de verdade no SER
/// (<c>ChegadaConfirmada</c>) e na agenda local (<c>Faltou</c>/<c>Realizado</c>). O SISREG não
/// entrega comparecimento consultável (ADR-0040), então uma solicitação do SISREG nunca vira
/// <see cref="Faltou"/> — no máximo <see cref="Compareceu"/> quando a recepção registrou a
/// chegada presencial (<c>AutorizadoEm</c>).</para>
/// </summary>
public enum SituacaoAgendamentoPaciente
{
    /// <summary>Na fila de regulação, ainda sem agenda.</summary>
    EmFila = 1,

    /// <summary>Pendente (aguardando regulação/análise).</summary>
    Pendente = 2,

    /// <summary>Agendado, aguardando o dia.</summary>
    Agendado = 3,

    /// <summary>Agendado e confirmado pelo paciente/unidade.</summary>
    Confirmado = 4,

    /// <summary>Paciente compareceu / atendimento realizado.</summary>
    Compareceu = 5,

    /// <summary>Chegada não confirmada (SER) — pode indicar não comparecimento.</summary>
    ChegadaNaoConfirmada = 6,

    /// <summary>Paciente faltou (não comparecimento registrado).</summary>
    Faltou = 7,

    /// <summary>Cancelado.</summary>
    Cancelado = 8,

    /// <summary>Concluído / alta.</summary>
    Concluido = 9,
}

/// <summary>Uma linha da aba "Agendamentos" do paciente, já normalizada entre as fontes.</summary>
/// <param name="Id">Id da linha na tabela de origem (não é chave global).</param>
/// <param name="Origem">Sistema que regulou.</param>
/// <param name="Tipo">"Consulta" ou "Exame".</param>
/// <param name="Descricao">Procedimento/recurso/especialidade, como texto de exibição.</param>
/// <param name="Unidade">Unidade executora, quando conhecida.</param>
/// <param name="DataHora">Data/hora do agendamento em horário de Brasília. <c>null</c> quando a
/// fonte não tem data (ex.: SER em fila, ou SISREG ainda em análise).</param>
/// <param name="TemHora">Indica se <see cref="DataHora"/> carrega hora real ou só a data.</param>
/// <param name="DataSolicitacao">Data em que o pedido entrou na fila/foi solicitado. Serve de
/// referência quando não há <see cref="DataHora"/> — a UI mostra "em fila desde…" / "solicitado
/// em…" em vez de "sem data". <c>null</c> para a agenda local (não tem esse eixo).</param>
/// <param name="Situacao">Situação normalizada.</param>
/// <param name="SituacaoDescricao">Rótulo pronto para a UI.</param>
/// <param name="SituacaoOrigem">Situação crua da fonte (para tooltip/auditoria).</param>
/// <param name="NumeroSolicitacao">Número da solicitação na origem — o "ID Solicitação" do SER,
/// ou o código SISREG da nossa <c>solicitacao</c>. <c>null</c> na agenda local.</param>
/// <param name="DetalheId">Id para abrir o detalhe (modal) da linha. SER: id da
/// <c>ser_solicitacao</c>; SISREG: id do <c>ExameImagem</c> (só existe para exame de imagem —
/// consultas ficam sem detalhe). <c>null</c> = linha sem detalhe navegável.</param>
public sealed record AgendamentoPacienteItemDto(
    Guid Id,
    OrigemAgendamentoPaciente Origem,
    string Tipo,
    string Descricao,
    string? Unidade,
    DateTime? DataHora,
    bool TemHora,
    DateOnly? DataSolicitacao,
    SituacaoAgendamentoPaciente Situacao,
    string SituacaoDescricao,
    string? SituacaoOrigem,
    string? NumeroSolicitacao,
    Guid? DetalheId);

/// <summary>Agendamentos do paciente, separados em próximos (por vir) e histórico (passados).</summary>
/// <param name="Proximos">Futuros/pendentes, do mais próximo para o mais distante.</param>
/// <param name="Historico">Passados/encerrados, do mais recente para o mais antigo.</param>
public sealed record AgendamentosPacienteDto(
    IReadOnlyList<AgendamentoPacienteItemDto> Proximos,
    IReadOnlyList<AgendamentoPacienteItemDto> Historico);
