namespace SMSMais.Core.Telefonia.Dtos;

/// <summary>Softphone de um usuário, como o admin e o próprio usuário o veem. Sem segredo.</summary>
public sealed record SoftphoneUsuarioDto(
    bool Habilitado,
    string? Ramal,
    string? NomeExibicao,
    bool Ativo);

/// <summary>Habilita ou altera o softphone de um usuário (admin, na edição do usuário).</summary>
public sealed record DefinirSoftphoneRequest(string Ramal, string NomeExibicao, bool Ativo = true);

public sealed record RamaisLivresDto(int? Inicio, int? Fim, IReadOnlyList<string> Sugestoes);

public sealed record IceServerDto(string Urls, string? Username, string? Credential);

/// <summary>
/// O que o navegador do próprio usuário precisa para registrar o softphone. Contém a senha SIP
/// em claro: sai só para o dono, com <c>Cache-Control: no-store</c>, e nunca é logada.
/// </summary>
public sealed record CredencialSoftphoneDto(
    string Ramal,
    string UsuarioSip,
    string Senha,
    string Dominio,
    string Uri,
    string WssUrl,
    string? NomeExibicao,
    IReadOnlyList<IceServerDto> IceServers);
