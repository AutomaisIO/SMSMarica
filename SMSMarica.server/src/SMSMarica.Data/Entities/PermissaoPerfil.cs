using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Liga um <see cref="Perfil"/> a um módulo e o conjunto de ações
/// liberadas dentro desse módulo.
/// </summary>
public class PermissaoPerfil
{
    public Guid PerfilId { get; set; }
    public Perfil Perfil { get; set; } = null!;
    public ModuloPermissao Modulo { get; set; }
    public AcoesPermissao Acoes { get; set; }
}
