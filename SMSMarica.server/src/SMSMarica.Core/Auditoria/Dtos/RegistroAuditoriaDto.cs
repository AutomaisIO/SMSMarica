namespace SMSMarica.Core.Auditoria.Dtos;

/// <summary>Uma linha da trilha de auditoria.</summary>
public sealed record RegistroAuditoriaDto(
    Guid Id,
    string Entidade,
    string EntidadeId,
    string Acao,
    string? ValorAnterior,
    string? ValorNovo,
    Guid? UsuarioId,
    string? UsuarioNome,
    DateTime CriadoEm);

/// <summary>Filtros da busca de auditoria (todos opcionais, combináveis).</summary>
public sealed record AuditoriaFiltroDto(
    string? Entidade = null,
    string? EntidadeId = null,
    Guid? UsuarioId = null,
    string? Texto = null,
    DateTime? De = null,
    DateTime? Ate = null,
    int Pagina = 1,
    int Tamanho = 50);

/// <summary>Página de resultados da busca de auditoria.</summary>
public sealed record PaginaAuditoriaDto(
    IReadOnlyList<RegistroAuditoriaDto> Itens,
    int Total,
    int Pagina,
    int Tamanho);
