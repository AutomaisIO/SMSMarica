namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Filtro da listagem de usuários. <see cref="Busca"/> casa por nome (ILIKE) ou CPF
/// (só dígitos). <see cref="UnidadeId"/> restringe aos usuários vinculados à unidade
/// (usuario_unidade). <see cref="Limite"/> limita o resultado (a lista tende a crescer).
/// </summary>
public sealed record FiltroUsuariosDto(
    string? Busca = null,
    Guid? UnidadeId = null,
    int Limite = 50);
