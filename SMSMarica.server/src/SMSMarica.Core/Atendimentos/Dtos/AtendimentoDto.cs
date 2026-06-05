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
    IReadOnlyList<DocumentoDto> Documentos);

/// <summary>Diagnóstico (Condition / CID-10) vinculado a um atendimento.</summary>
public sealed record DiagnosticoDto(string Codigo, string? Descricao);

/// <summary>Medicamento prescrito (MedicationRequest) vinculado a um atendimento.</summary>
public sealed record MedicamentoDto(Guid Id, string Descricao, string? Posologia, bool Urgente);

/// <summary>Documento clínico (DocumentReference) com o conteúdo HTML já decodificado.</summary>
public sealed record DocumentoDto(Guid Id, string Tipo, DateTimeOffset? Data, string ConteudoHtml);
