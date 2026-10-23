namespace SMSMais.Core.FaturamentoProducao.Dtos;

/// <summary>Unidade liberada para o usuário final gerar BPA (habilitada ∩ escopo).</summary>
public sealed record UnidadeHabilitadaDto(Guid Id, string Nome, string? Cnes);

/// <summary>Linha da tela de Configurações: unidade do escopo + se está habilitada.</summary>
public sealed record ConfigUnidadeDto(Guid Id, string Nome, string? Cnes, bool Habilitada);

/// <summary>Corpo do PUT que liga/desliga uma unidade.</summary>
public sealed record DefinirUnidadeHabilitadaRequest(bool Habilitada);

/// <summary>Uma linha do preview do BPA-I (com os campos para conferência + status de qualidade).</summary>
public sealed record BpaLinhaDto(
    int? Folha,
    int? Sequencia,
    string? ProcedimentoSigtap,
    string? ProcedimentoTexto,
    string? Cid,
    string? CnsProfissional,
    string? Cbo,
    string? ProfissionalNome,
    string PacienteNome,
    string? Cns,
    string? Cpf,
    DateOnly? Nascimento,
    string Sexo,
    DateOnly? DataAtendimento,
    bool Valida,
    IReadOnlyList<string> Motivos);

public sealed record MotivoContagemDto(string Motivo, int Quantidade);

/// <summary>Estatísticas de qualidade/cobertura da competência (como o parecer de viabilidade).</summary>
public sealed record BpaEstatisticasDto(
    int Total,
    int Validas,
    int Pendencias,
    int ComSigtap,
    int ComCid,
    int ComCnsProfissional,
    int ComCnsPaciente,
    int ComCpfPaciente,
    int ComSexo,
    IReadOnlyList<MotivoContagemDto> MotivosPendencia);

/// <summary>Preview completo: cabeçalho + estatísticas + linhas (válidas e pendências).</summary>
public sealed record BpaPreviewDto(
    Guid UnidadeId,
    string UnidadeNome,
    string? Cnes,
    int Competencia,
    int Folhas,
    int Controle,
    BpaEstatisticasDto Estatisticas,
    IReadOnlyList<BpaLinhaDto> Linhas);
