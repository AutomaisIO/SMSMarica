namespace SMSMais.Core.Anamneses.Dtos;

/// <summary>Anamnese persistida (conteúdo JSON versionado por tipo de questionário).</summary>
public sealed record AnamneseDto(
    Guid Id,
    Guid SolicitacaoExameId,
    string Tipo,
    int Versao,
    string ConteudoJson,
    string? ClassificacaoRisco,
    string? PreenchidoPorNome,
    DateTime CriadoEm,
    DateTime? AtualizadoEm);

/// <summary>
/// Contexto para abrir a tela de anamnese: identifica o pedido + paciente
/// (seção 1 do questionário, somente leitura) e traz a anamnese existente
/// (null = ainda não preenchida).
/// </summary>
public sealed record AnamneseContextoDto(
    Guid SolicitacaoExameId,
    string AccessionNumber,
    string TipoExameNome,
    string ModalidadeDicom,
    Guid PacienteId,
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    DateOnly? PacienteNascimento,
    AnamneseDto? Anamnese);

/// <summary>Payload de criação/edição (upsert) da anamnese de uma solicitação.</summary>
public sealed record SalvarAnamneseDto(
    string Tipo,
    int Versao,
    string ConteudoJson,
    string? ClassificacaoRisco);
