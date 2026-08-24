namespace SMSMais.Core.Identidade;

/// <summary>
/// Acessor do usuário autenticado na requisição corrente. Usado por services para
/// gravar auditoria (CriadoPor, AtualizadoPor, ExcluidoPor) sem acoplar Core ao
/// ASP.NET. Implementação vive em <c>SMSMais.Api/Auth/UsuarioAtualAccessor</c>.
/// </summary>
public interface IUsuarioAtualAccessor
{
    /// <summary>
    /// Id do usuário do token JWT da requisição, ou <c>null</c> se não houver
    /// contexto autenticado (ex.: tarefas em background, seed).
    /// </summary>
    Guid? UsuarioId { get; }

    /// <summary>
    /// Unidade ativa declarada pelo cliente via header <c>X-Unidade-Id</c>, ou <c>null</c>
    /// se ausente/inválido. NÃO é validada contra os vínculos do usuário aqui — a
    /// validação é responsabilidade do service que a consome.
    /// </summary>
    Guid? UnidadeAtivaId { get; }

    /// <summary>IP real do cliente na requisição (X-Forwarded-For já honrado), ou <c>null</c>
    /// fora de uma requisição (jobs/seed). Para a trilha de auditoria.</summary>
    string? Ip { get; }

    /// <summary>
    /// Identidade da SESSÃO (o <c>jti</c> do token), não do usuário: muda a cada login.
    ///
    /// <para>Serve para amarrar estado de memória ao ciclo de vida da sessão — hoje, a sessão de
    /// escrita no SER. Chavear por usuário faria o operador que saiu e voltou <b>herdar</b> a
    /// credencial da sessão anterior; chavear por <c>jti</c> faz sair-e-entrar valer o que
    /// aparenta valer.</para>
    /// </summary>
    string? SessaoId { get; }
}
