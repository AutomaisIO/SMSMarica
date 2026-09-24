namespace SMSMais.Core.Laudos.Assinatura.Nuvem;

/// <summary>
/// Cliente da API IntegraICP v3 (ADR-0061). Três passos, amarrados por PKCE (RFC 7636):
/// autorização (o médico aprova no app do PSC) → credencial (certificado) → assinatura do hash.
/// </summary>
public interface IIntegraIcpClient
{
    /// <summary>
    /// Abre a autorização para o CPF e devolve a URL que o médico abre para aprovar no app.
    /// Lança <see cref="Common.Excecoes.ConflitoException"/> se o CPF não tiver certificado
    /// em nenhum PSC do canal.
    /// </summary>
    Task<string> IniciarAutorizacaoAsync(
        string cpf, string urlRetorno, string codeChallenge, CancellationToken cancellationToken = default);

    /// <summary>Certificado (DER) do titular da credencial autorizada.</summary>
    Task<byte[]> ObterCertificadoAsync(
        string credencialId, string codeVerifier, CancellationToken cancellationToken = default);

    /// <summary>Assinatura crua (política RAW) do <paramref name="hash"/> SHA-256.</summary>
    Task<byte[]> AssinarHashAsync(
        string credencialId, string codeVerifier, byte[] hash, CancellationToken cancellationToken = default);
}
