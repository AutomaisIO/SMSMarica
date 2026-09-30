using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Tratamentos.Dtos;

/// <summary>Cadastro de um atendimento do Transporte de Pacientes. O tempo médio vem do tipo; o
/// horário de busca, da rota — nenhum dos dois é informado aqui.</summary>
public sealed record CadastrarTratamentoRequest(
    Guid PacienteId,
    Guid UnidadeAtendimentoId,
    /// <summary>Obrigatório (o validador cobra): é dele que vem o tempo médio.</summary>
    Guid? TipoTratamentoId,
    string Descricao,
    string? Observacoes,
    AgendaRequest Agenda,
    NecessidadesRequest Necessidades,
    RegraAcompanhantesRequest Acompanhantes);

/// <summary>
/// Agenda do atendimento: a partir de <paramref name="DataInicio"/>, nos dias marcados em
/// <paramref name="DiasSemanaMascara"/> (bit 0 = domingo … bit 6 = sábado), ou
/// <paramref name="QuantidadeSessoes"/> sessões ou <paramref name="Continuo"/> (renova todo mês).
/// Também é o corpo da prévia (<c>POST /tratamentos/agenda/previa</c>).
/// </summary>
public sealed record AgendaRequest(
    DateOnly DataInicio,
    int DiasSemanaMascara,
    int? QuantidadeSessoes,
    bool Continuo);

/// <summary>Condição do paciente para a viagem. Por enquanto só registro: quem monta a rota lê.</summary>
public sealed record NecessidadesRequest(
    MobilidadeTransporte Mobilidade,
    bool DificuldadeVeiculoAlto,
    bool Isolamento,
    bool UsaOxigenio,
    bool NecessitaAjuda,
    string? AjudaDescricao);

/// <summary>Quantos acompanhantes podem ir: 1 por direito; 2 exige a justificativa da liberação.</summary>
public sealed record RegraAcompanhantesRequest(
    int Quantidade,
    string? JustificativaSegundo);
