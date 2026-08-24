namespace SMSMais.Data;

/// <summary>
/// Identificadores fixos usados em seeds e referências cruzadas. Mantê-los
/// estáveis entre migrations/ambientes — não regenerar.
/// </summary>
public static class IdentificadoresFixos
{
    /// <summary>Perfil "Administrador" semeado no startup (UUID com 1's para reconhecer em dev).</summary>
    public static readonly Guid PerfilAdminId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>Usuário "Administrador" semeado no startup (mesmo padrão).</summary>
    public static readonly Guid UsuarioAdminId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>Template de laudo "Mamografia Digital Bilateral (CDT)" semeado no startup.</summary>
    public static readonly Guid TemplateMamografiaCdtId = new("22222222-2222-2222-2222-222222222001");
}
