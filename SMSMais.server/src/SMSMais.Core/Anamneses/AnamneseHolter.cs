using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Anamneses;

/// <summary>
/// A anamnese do Holter: altura, peso e fumante — o que o CardioNet pede na worklist (0010,1020),
/// (0010,1030) e (0010,21A0) e hoje recebe vazio. Diferente da mamografia, ela ALIMENTA o item de
/// worklist; por isso o exame só vai à worklist depois dela, e corrigi-la regrava o item.
///
/// <para>Qual questionário vale sai da modalidade do tipo de exame: ECG é o Holter (o
/// eletrocardiograma ficou "Não configurada" em 20/10/2026, e a MAPA é OT). Se um dia outro exame
/// ECG passar a ter aparelho, a escolha muda para o cadastro do equipamento.</para>
/// </summary>
public static class AnamneseHolter
{
    public const string Tipo = "holter";
    public const int Versao = 1;

    /// <summary>O questionário do exame: "holter" ou "mamografia" (o de sempre).</summary>
    public static string QuestionarioDa(ModalidadeDicom? modalidade) => Questionario(EhHolter(modalidade));

    /// <summary>O questionário do pedido: "holter" quando ele tem Holter, senão "mamografia".</summary>
    public static string Questionario(bool holter) => holter ? Tipo : "mamografia";

    public static bool EhHolter(ModalidadeDicom? modalidade) => modalidade == ModalidadeDicom.ECG;

    /// <summary>Conteúdo gravado em <c>Anamnese.ConteudoJson</c>.</summary>
    /// <param name="Fumante">"sim", "nao" ou "parou".</param>
    public sealed record Conteudo(
        [property: JsonPropertyName("alturaCm")] int? AlturaCm,
        [property: JsonPropertyName("pesoKg")] decimal? PesoKg,
        [property: JsonPropertyName("fumante")] string? Fumante);

    private static readonly string[] RespostasFumante = ["sim", "nao", "parou"];

    /// <summary>Lê e valida o conteúdo. A trava é aqui, e não só na tela: é o que vai ao aparelho.</summary>
    public static Conteudo Validar(string conteudoJson)
    {
        Conteudo? c;
        try { c = JsonSerializer.Deserialize<Conteudo>(conteudoJson); }
        catch (JsonException) { c = null; }
        if (c is null)
            throw new ValidacaoException("anamnese.holter_invalida", "Conteúdo da anamnese do Holter ilegível.");

        if (c.AlturaCm is not (>= 50 and <= 250))
            throw new ValidacaoException("anamnese.holter_altura", "Informe a altura em centímetros (entre 50 e 250).");
        if (c.PesoKg is not (>= 2 and <= 400))
            throw new ValidacaoException("anamnese.holter_peso", "Informe o peso em quilos (entre 2 e 400).");
        if (c.Fumante is null || !RespostasFumante.Contains(c.Fumante))
            throw new ValidacaoException("anamnese.holter_fumante", "Informe se o paciente é fumante: sim, não ou parou.");

        return c;
    }

    /// <summary>
    /// O que vai à worklist. "Parou" sai como NO: o Smoking Status (0010,21A0) só tem YES, NO e
    /// UNKNOWN, e a pergunta do aparelho é se o paciente fuma hoje. Conteúdo ilegível devolve null
    /// — o item vai sem os três campos, como ia antes.
    /// </summary>
    public static DadosClinicosWorklist? ParaWorklist(string conteudoJson)
    {
        Conteudo c;
        try { c = Validar(conteudoJson); }
        catch (ValidacaoException) { return null; }

        return new DadosClinicosWorklist(
            AlturaMetros: Math.Round(c.AlturaCm!.Value / 100m, 2),
            PesoKg: c.PesoKg!.Value,
            SmokingStatus: c.Fumante == "sim" ? "YES" : "NO");
    }

    /// <summary>Número no formato do VR DS (ponto decimal, sem separador de milhar).</summary>
    internal static string Ds(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
