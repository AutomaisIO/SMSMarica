namespace SMSMarica.Core.Mapeamento.Dtos;

/// <summary>Um procedimento SIGTAP de imagem pendente de mapeamento (exames importados sem
/// TipoExame), com o texto do SISREG e quantos exames estão esperando.</summary>
public sealed record PendenteMapeamentoDto(
    string SigtapCodigo,
    string? ProcedimentoTexto,
    int Quantidade);

/// <summary>Vincula um TipoExame existente a todos os exames de imagem pendentes com este SIGTAP.</summary>
public sealed record VincularMapeamentoRequest(
    string SigtapCodigo,
    Guid TipoExameId);

public sealed record VincularMapeamentoResultado(int Atualizados);
