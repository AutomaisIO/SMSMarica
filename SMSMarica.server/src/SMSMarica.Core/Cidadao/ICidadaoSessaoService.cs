namespace SMSMarica.Core.Cidadao;

/// <summary>
/// Gerencia a identidade de acesso do cidadão (<c>cidadao_acesso</c>) e a
/// <b>sessão única por dispositivo</b> (<c>cidadao_sessao</c>). Toda autenticação
/// (OTP/senha/social) passa por <see cref="AbrirSessaoAsync"/>, que revoga a sessão
/// ativa anterior e emite um token novo — o aparelho antigo perde o acesso.
/// </summary>
public interface ICidadaoSessaoService
{
    /// <summary>
    /// Abre uma sessão para o cidadão (criando o <c>cidadao_acesso</c> se ainda não existir),
    /// revogando qualquer sessão ativa anterior. Retorna o JWT e a expiração.
    /// </summary>
    Task<(string Token, DateTime ExpiraEm)> AbrirSessaoAsync(
        Guid patientId, string nome, string cpf, string canal,
        string? dispositivo, string? ip, CancellationToken ct = default);

    /// <summary>Valida o jti (id da sessão) contra a sessão ativa do paciente. Usado a cada request.</summary>
    Task<bool> SessaoValidaAsync(Guid sessaoJti, Guid patientId, CancellationToken ct = default);

    /// <summary>Revoga a sessão atual (logout).</summary>
    Task RevogarAsync(Guid sessaoJti, CancellationToken ct = default);
}
