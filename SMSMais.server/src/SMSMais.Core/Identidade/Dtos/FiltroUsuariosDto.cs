namespace SMSMais.Core.Identidade.Dtos;

/// <summary>
/// Filtro da listagem de usuários. <see cref="Busca"/> casa por nome (ILIKE) ou CPF
/// (só dígitos). <see cref="UnidadeId"/> restringe aos usuários vinculados à unidade
/// (usuario_unidade). <see cref="SemUnidadePadrao"/> traz só quem ainda não tem unidade padrão —
/// a pendência a resolver, já que a padrão é obrigatória (ADR-0081). <see cref="Limite"/> limita o
/// resultado (a lista tende a crescer).
/// </summary>
public sealed record FiltroUsuariosDto(
    string? Busca = null,
    Guid? UnidadeId = null,
    int Limite = 50,
    bool SemUnidadePadrao = false);
