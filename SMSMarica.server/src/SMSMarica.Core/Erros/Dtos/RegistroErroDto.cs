namespace SMSMarica.Core.Erros.Dtos;

/// <summary>Dados de um erro para exibição/pesquisa no diagnóstico.</summary>
public sealed record RegistroErroDto(
    Guid Id,
    string CodigoReferencia,
    DateTime CriadoEm,
    string Metodo,
    string Caminho,
    string? QueryString,
    int StatusCode,
    string TipoExcecao,
    string Mensagem,
    string? StackTrace,
    string? Interna,
    string? TraceId,
    Guid? UsuarioId,
    string? UsuarioNome,
    string? UserAgent);

/// <summary>Item de lista (sem stack trace, para a grade de busca).</summary>
public sealed record RegistroErroListItemDto(
    Guid Id,
    string CodigoReferencia,
    DateTime CriadoEm,
    string Metodo,
    string Caminho,
    int StatusCode,
    string TipoExcecao,
    string Mensagem,
    string? UsuarioNome);

/// <summary>Dados capturados pelo middleware ao registrar um erro não tratado.</summary>
public sealed record RegistrarErroDados(
    string Metodo,
    string Caminho,
    string? QueryString,
    int StatusCode,
    string TipoExcecao,
    string Mensagem,
    string? StackTrace,
    string? Interna,
    string? TraceId,
    string? UserAgent);

public sealed record ErroFiltroDto(
    string? Codigo = null,
    string? Texto = null,
    DateTime? De = null,
    DateTime? Ate = null,
    int Pagina = 1,
    int Tamanho = 50);

public sealed record PaginaErrosDto(
    IReadOnlyList<RegistroErroListItemDto> Itens,
    int Total,
    int Pagina,
    int Tamanho);
