using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Sisreg;

/// <summary>
/// Configuração SISREG resolvida para uso interno do cliente HTTP: já com a base URL
/// normalizada, as centrais reguladoras separadas e os segredos (senha/token) revelados.
/// Não sai para a API — apenas circula entre <c>SisregConfiguracaoService</c> e <c>SisregClient</c>.
/// </summary>
public sealed record SisregContexto(
    string BaseUrl,
    EscopoSisreg Escopo,
    string Uf,
    string Municipio,
    IReadOnlyList<string> CentraisReguladoras,
    TipoAutenticacaoSisreg TipoAutenticacao,
    string? Login,
    string? Senha,
    string? Token);
