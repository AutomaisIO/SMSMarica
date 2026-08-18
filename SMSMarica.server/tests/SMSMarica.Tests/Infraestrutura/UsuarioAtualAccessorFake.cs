using SMSMarica.Core.Identidade;

namespace SMSMarica.Tests.Infraestrutura;

/// <summary>
/// Fake do <see cref="IUsuarioAtualAccessor"/> para testes. Por padrão devolve
/// null (operação "sistema/sem contexto"). Construtor com argumento permite
/// simular um usuário autenticado específico.
/// </summary>
public sealed class UsuarioAtualAccessorFake(
    Guid? usuarioId = null,
    Guid? unidadeAtivaId = null,
    string? ip = null,
    string? sessaoId = null) : IUsuarioAtualAccessor
{
    public Guid? UsuarioId { get; } = usuarioId;
    public Guid? UnidadeAtivaId { get; } = unidadeAtivaId;
    public string? Ip { get; } = ip;

    /// <summary>Acompanha o usuário por padrão: quem simula "autenticado" quer os dois.</summary>
    public string? SessaoId { get; } = sessaoId ?? usuarioId?.ToString("N");
}
