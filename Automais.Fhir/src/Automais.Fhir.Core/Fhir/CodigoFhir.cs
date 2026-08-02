using Hl7.Fhir.Utility;

namespace Automais.Fhir.Core.Fhir;

/// <summary>
/// Converte enum do modelo Firely para o <b>código FHIR</b> que vai nas colunas de busca.
///
/// <para><b>Por que existe.</b> <c>Status.ToString().ToLowerInvariant()</c> devolve o nome do
/// enum .NET, não o código do ValueSet: <c>EncounterStatus.InProgress</c> vira
/// <c>"inprogress"</c> quando o código correto é <c>"in-progress"</c>. O JSONB (serializado
/// pelo Firely) sempre esteve certo — quem ficava errada era a coluna de busca, e aí
/// <c>?status=in-progress</c> não achava nada.</para>
///
/// <para>O bug ficou dormente por mais de um milhão de registros porque todo status usado até
/// então era palavra única (<c>finished</c>, <c>active</c>), que sobrevive ao ToString por
/// acidente. Apareceu no dia em que a internação (ADR-0025) trouxe o primeiro código com
/// hífen. <see cref="EnumUtility.GetLiteral"/> lê o atributo do próprio ValueSet, então vale
/// para qualquer código — inclusive os que ainda não usamos.</para>
/// </summary>
public static class CodigoFhir
{
    /// <summary>Código FHIR do enum (ex.: <c>in-progress</c>).</summary>
    public static string De<T>(T valor) where T : struct, Enum => EnumUtility.GetLiteral(valor);

    /// <summary>Idem, tolerando ausência — é a forma usada ao extrair search params.</summary>
    public static string? De<T>(T? valor) where T : struct, Enum =>
        valor is { } v ? De(v) : null;
}
