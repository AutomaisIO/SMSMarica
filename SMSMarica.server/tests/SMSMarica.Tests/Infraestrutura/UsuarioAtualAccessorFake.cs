using SMSMarica.Core.Identidade;

namespace SMSMarica.Tests.Infraestrutura;

/// <summary>
/// Fake do <see cref="IUsuarioAtualAccessor"/> para testes. Por padrão devolve
/// null (operação "sistema/sem contexto"). Construtor com argumento permite
/// simular um usuário autenticado específico.
/// </summary>
public sealed class UsuarioAtualAccessorFake(Guid? usuarioId = null, Guid? unidadeAtivaId = null) : IUsuarioAtualAccessor
{
    public Guid? UsuarioId { get; } = usuarioId;
    public Guid? UnidadeAtivaId { get; } = unidadeAtivaId;
}
