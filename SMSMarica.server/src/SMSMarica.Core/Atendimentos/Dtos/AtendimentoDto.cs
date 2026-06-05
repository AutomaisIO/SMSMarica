namespace SMSMarica.Core.Atendimentos.Dtos;

/// <summary>Um atendimento do paciente (Encounter do hub FHIR) com seus diagnósticos.</summary>
public sealed record AtendimentoDto(
    Guid Id,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    string Tipo,
    string Status,
    string? MedicoNome,
    string? Fonte,
    IReadOnlyList<DiagnosticoDto> Diagnosticos,
    IReadOnlyList<MedicamentoDto> Medicamentos,
    IReadOnlyList<DocumentoDto> Documentos,
    IReadOnlyList<SinalVitalDto> SinaisVitais,
    RiscoDto? Risco);

/// <summary>
/// Sinal vital aferido (Observation, perfil vital-signs) vinculado a um atendimento.
/// <see cref="Valor2"/> só é usado na pressão arterial (sistólica em <see cref="Valor"/>,
/// diastólica em <see cref="Valor2"/>).
/// </summary>
public sealed record SinalVitalDto(
    string Codigo,
    string Nome,
    double? Valor,
    double? Valor2,
    string? Unidade,
    DateTimeOffset? Em);

/// <summary>Classificação de risco (cor da triagem) do atendimento.</summary>
public sealed record RiscoDto(string Cor, string? Descricao, DateTimeOffset? Em);

/// <summary>Diagnóstico (Condition / CID-10) vinculado a um atendimento.</summary>
public sealed record DiagnosticoDto(string Codigo, string? Descricao);

/// <summary>Medicamento prescrito (MedicationRequest) vinculado a um atendimento.</summary>
public sealed record MedicamentoDto(Guid Id, string Descricao, string? Posologia, bool Urgente);

/// <summary>Documento clínico (DocumentReference) com o conteúdo HTML já decodificado.</summary>
public sealed record DocumentoDto(Guid Id, string Tipo, DateTimeOffset? Data, string ConteudoHtml);
