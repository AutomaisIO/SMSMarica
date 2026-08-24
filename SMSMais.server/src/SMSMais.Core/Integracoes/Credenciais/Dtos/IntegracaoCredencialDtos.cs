namespace SMSMais.Core.Integracoes.Credenciais.Dtos;

/// <summary>
/// Credencial de provedor exposta na tela. Nunca devolve client_id/client_secret:
/// apenas sinaliza se já estão definidos (<see cref="ClientIdDefinido"/> /
/// <see cref="ClientSecretDefinido"/>). RedirectUri e ParametrosJson são públicos.
/// </summary>
public sealed record IntegracaoCredencialDto(
    string Provedor,
    string Rotulo,
    bool ClientIdDefinido,
    bool ClientSecretDefinido,
    string? RedirectUri,
    string? ParametrosJson,
    bool Ativo);

/// <summary>
/// Upsert de credencial de provedor. client_id/client_secret vazios = mantém o atual;
/// preenchidos = cifra e substitui (padrão write-only).
/// </summary>
public sealed record AtualizarIntegracaoCredencialRequest(
    string? ClientId,
    string? ClientSecret,
    string? RedirectUri,
    string? ParametrosJson,
    bool Ativo);
