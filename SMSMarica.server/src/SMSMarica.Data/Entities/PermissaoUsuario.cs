using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Override individual de permissão (sempre aditivo). Concede ações
/// extras que não vêm dos perfis do usuário — nunca remove o que já
/// é herdado.
/// </summary>
public class PermissaoUsuario
{
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public ModuloPermissao Modulo { get; set; }
    public AcoesPermissao Acoes { get; set; }
}
