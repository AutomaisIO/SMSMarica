namespace SMSMarica.Core.Identidade;

/// <summary>
/// Acessor do usuário autenticado na requisição corrente. Usado por services para
/// gravar auditoria (CriadoPor, AtualizadoPor, ExcluidoPor) sem acoplar Core ao
/// ASP.NET. Implementação vive em <c>SMSMarica.Api/Auth/UsuarioAtualAccessor</c>.
/// </summary>
public interface IUsuarioAtualAccessor
{
    /// <summary>
    /// Id do usuário do token JWT da requisição, ou <c>null</c> se não houver
    /// contexto autenticado (ex.: tarefas em background, seed).
    /// </summary>
    Guid? UsuarioId { get; }
}
