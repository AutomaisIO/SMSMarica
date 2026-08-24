namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Esquema de autenticação usado contra a API-SISREG (Elasticsearch). O manual v2.1
/// não fixa o esquema (credenciais são enviadas por e-mail pelo DATASUS na homologação);
/// mantemos as três possibilidades comuns para plugar quando o acesso sair.
/// </summary>
public enum TipoAutenticacaoSisreg
{
    /// <summary>HTTP Basic — <c>Authorization: Basic base64(login:senha)</c>. Default do Elasticsearch.</summary>
    Basic = 1,

    /// <summary>Bearer token — <c>Authorization: Bearer {token}</c>.</summary>
    Bearer = 2,

    /// <summary>API Key do Elasticsearch — <c>Authorization: ApiKey {token}</c>.</summary>
    ApiKey = 3,
}
