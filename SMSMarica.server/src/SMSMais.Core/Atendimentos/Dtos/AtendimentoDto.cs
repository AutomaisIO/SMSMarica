namespace SMSMais.Core.Atendimentos.Dtos;

/// <summary>
/// Um atendimento do paciente (Encounter do hub FHIR) com seus diagnósticos.
///
/// <para><b><see cref="Fonte"/> e <see cref="UnidadeNome"/> são dimensões ortogonais</b>
/// (ADR-0039): a fonte diz qual PEP gerou o registro, a unidade diz onde o paciente foi
/// atendido. Uma não deriva da outra — a base <c>salux-hcml</c> serve as três unidades, e a
/// mesma unidade tem atendimentos de dois sistemas por causa do cutover Salux→Klinikos. Ler
/// unidade de dentro do <c>meta.source</c> daria "HMCML" para 431 mil atendimentos que
/// aconteceram na UPA Inoã e na Santa Rita.</para>
/// </summary>
public sealed record AtendimentoDto(
    Guid Id,
    DateTimeOffset? Inicio,
    DateTimeOffset? Fim,
    string Tipo,
    string Status,
    string? MedicoNome,
    string? Fonte,
    /// <summary>Nome da unidade executante (<c>Encounter.serviceProvider</c> → Organization).</summary>
    string? UnidadeNome,
    /// <summary>CNES da unidade — chave estável para rotular, já que o nome oficial é longo.</summary>
    string? UnidadeCnes,
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
