namespace SMSMais.Core.Identidade.Dtos;

public sealed record LoginRespostaDto(
    string Token,
    DateTime ExpiraEm,
    UsuarioDto Usuario,
    IReadOnlyList<PermissaoModuloDto> Permissoes,
    IReadOnlyList<UnidadeVinculadaDto> Unidades);

/// <summary>Unidade vinculada ao usuário (usuario_unidade), só as ativas.</summary>
public sealed record UnidadeVinculadaDto(Guid Id, string Nome, bool Principal);
