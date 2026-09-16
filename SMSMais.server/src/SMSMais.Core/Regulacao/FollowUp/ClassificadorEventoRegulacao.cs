using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.FollowUp;

/// <summary>
/// Tipa o verbo da trilha do SER/SERNIT (<see cref="TipoEventoExterno"/>) e dá identidade ao
/// conjunto de regras de FollowUP. Função pura, sem banco.
///
/// <para><b>Por que uma só regra de verbo para os dois sistemas:</b> SER e SERNIT são irmãos e
/// escrevem os mesmos verbos. Manter duas listas seria convidar a divergência que já existia entre
/// o sincronizador e a listagem de notificações.</para>
/// </summary>
public static class ClassificadorEventoRegulacao
{
    /// <summary>
    /// O SER escreve "FollowUP"; a tolerância a caixa, hífen e espaço vem do sincronizador
    /// original. O backfill em SQL da migration usa o equivalente <c>ILIKE '%follow%up%'</c>.
    /// </summary>
    public static bool EhFollowUp(string? verbo) =>
        verbo is not null
        && verbo.Replace("-", string.Empty).Replace(" ", string.Empty)
            .Contains("followup", StringComparison.OrdinalIgnoreCase);

    public static TipoEventoExterno TipoDoVerbo(string? verbo)
    {
        if (string.IsNullOrWhiteSpace(verbo)) return TipoEventoExterno.Outro;
        if (EhFollowUp(verbo)) return TipoEventoExterno.FollowUp;

        var v = verbo.Trim();
        if (v.StartsWith("solicit", StringComparison.OrdinalIgnoreCase)) return TipoEventoExterno.Solicitar;
        if (v.StartsWith("pendenc", StringComparison.OrdinalIgnoreCase)) return TipoEventoExterno.Pendenciar;
        if (v.StartsWith("cancel", StringComparison.OrdinalIgnoreCase)) return TipoEventoExterno.Cancelar;
        if (v.StartsWith("agend", StringComparison.OrdinalIgnoreCase)) return TipoEventoExterno.Agendar;
        return TipoEventoExterno.Outro;
    }

    /// <summary>
    /// Identidade curta do conjunto de regras (16 hex do SHA-256). Cada evento classificado carrega
    /// o hash das regras que o classificaram: quando alguém edita as regras pela tela, o worker
    /// reclassifica só o que ficou com hash diferente — sem versão manual, sem esquecer de
    /// incrementar.
    ///
    /// <para>O hash é do JSON <b>canônico</b> (regras parseadas e reserializadas), não do texto
    /// gravado: a coluna é <c>jsonb</c>, e o Postgres devolve o JSON com espaços removidos e chaves
    /// reordenadas. Hash do texto cru mudaria a cada leitura e reclassificaria tudo à toa.</para>
    /// </summary>
    public static string HashDasRegras(string? regrasJson)
    {
        var canonico = JsonSerializer.Serialize(ClassificadorFollowUp.Ler(regrasJson));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonico));
        return Convert.ToHexStringLower(bytes)[..16];
    }
}
