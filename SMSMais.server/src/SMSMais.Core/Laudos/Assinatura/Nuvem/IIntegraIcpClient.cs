namespace SMSMais.Core.Laudos.Assinatura.Nuvem;

/// <summary>
/// Cliente da API IntegraICP v3 (ADR-0061). Três passos, amarrados por PKCE (RFC 7636):
/// autorização (o médico aprova no app do PSC) → credencial (certificado) → assinatura do hash.
/// </summary>
public interface IIntegraIcpClient
{
    /// <summary>
    /// Abre a autorização para o CPF e devolve a URL que o médico abre para aprovar no app.
    /// <paramref name="credencialVidaSegundos"/> é quanto a credencial aprovada vale (o
    /// <c>credential_lifetime</c>; trazido para dentro do que a API aceita).
    /// Lança <see cref="Common.Excecoes.ConflitoException"/> se o CPF não tiver certificado
    /// em nenhum PSC do canal.
    /// </summary>
    Task<string> IniciarAutorizacaoAsync(
        string cpf, string urlRetorno, string codeChallenge, int credencialVidaSegundos,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Certificado (DER) do titular da credencial autorizada. Esta etapa e a assinatura lançam
    /// <see cref="Common.Excecoes.ConflitoException"/> com código
    /// <see cref="IntegraIcpClient.CodigoCredencialInvalida"/> quando a credencial expirou ou
    /// foi recusada — é o sinal para pedir uma aprovação nova ao médico.
    /// </summary>
    Task<byte[]> ObterCertificadoAsync(
        string credencialId, string codeVerifier, CancellationToken cancellationToken = default);

    /// <summary>Assinatura crua (política RAW) do <paramref name="hash"/> SHA-256.</summary>
    Task<byte[]> AssinarHashAsync(
        string credencialId, string codeVerifier, byte[] hash, CancellationToken cancellationToken = default);
}
