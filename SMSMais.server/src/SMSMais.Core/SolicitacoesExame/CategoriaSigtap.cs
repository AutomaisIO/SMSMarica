using System.Globalization;
using System.Text;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.SolicitacoesExame;

/// <summary>
/// Roteamento determinístico da natureza clínica pelo SUBGRUPO SIGTAP (4 primeiros dígitos) —
/// não adivinha procedimento. Ver ADR-0021. Função pura, sem banco: mesmo critério para o
/// importador do SISREG e para o Processo Regulatório.
/// </summary>
public static class CategoriaSigtap
{
    // Termos (sem acento, MAIÚSCULO) que marcam um procedimento de IMAGEM pelo NOME do SISREG.
    // O eixo do catálogo é o NOME do SISREG, não o SIGTAP — o "SIGTAP" exportado é defasado e a
    // correlação oficial é trabalho de faturamento, à parte (ver ResolvedorTipoExameSisreg). Por
    // isso a categoria também sai do nome quando não há SIGTAP (ex.: a agenda do cons_agendas, que
    // não informa código nenhum, só "USG OBSTETRICA").
    private static readonly string[] TermosImagem =
    [
        "ULTRASSON", "ULTRASON", "USG", "ECOGRAFIA", "ECODOPPLER", "ECOCARDIO", "DOPPLER",
        "RADIOGRAFIA", "RAIO X", "RAIO-X", "TOMOGRAFIA", "RESSONANCIA", "MAMOGRAFIA", "MASTOGRAFIA",
        "DENSITOMETRIA", "ANGIOGRAFIA", "UROGRAFIA", "HISTEROSSALPINGO", "CINEANGIO",
    ];
    /// <summary>
    /// Categoria a partir do código SIGTAP (com ou sem pontuação). Código curto ou vazio cai em
    /// <see cref="CategoriaSolicitacao.Outro"/> — nunca lança.
    /// </summary>
    public static CategoriaSolicitacao Resolver(string? sigtap)
    {
        var d = SoDigitos(sigtap);
        if (d.Length < 4) return CategoriaSolicitacao.Outro;
        return d[..4] switch
        {
            "0301" or "0302" => CategoriaSolicitacao.Consulta,
            "0204" or "0205" or "0206" => CategoriaSolicitacao.Imagem, // RX/mamo/densito · US · TC/RM
            "0202" or "0203" => CategoriaSolicitacao.Laboratorio,      // lab clínico · patologia
            "0211" => CategoriaSolicitacao.GraficoFuncional,          // ECG, EEG, audiometria, espirometria...
            "0209" => CategoriaSolicitacao.Endoscopia,
            _ => d[..2] == "04" ? CategoriaSolicitacao.Cirurgia : CategoriaSolicitacao.Outro,
        };
    }

    /// <summary>
    /// Categoria a partir do NOME do procedimento do SISREG, quando não há SIGTAP para rotear (o
    /// caso da agenda do <c>cons_agendas</c>). Reconhece imagem por termo (US, RX, TC, RM, mamo…)
    /// e consulta por "CONSULTA"; o resto fica <see cref="CategoriaSolicitacao.Outro"/>. Não
    /// adivinha o procedimento — só a NATUREZA, para decidir o satélite de execução. Nunca lança.
    /// </summary>
    public static CategoriaSolicitacao ResolverPorNome(string? nome)
    {
        var n = SemAcento((nome ?? string.Empty).ToUpperInvariant());
        if (n.Length == 0) return CategoriaSolicitacao.Outro;
        if (TermosImagem.Any(t => n.Contains(t, StringComparison.Ordinal)))
            return CategoriaSolicitacao.Imagem;
        if (n.Contains("CONSULTA", StringComparison.Ordinal))
            return CategoriaSolicitacao.Consulta;
        return CategoriaSolicitacao.Outro;
    }

    /// <summary>
    /// Se a categoria cria satélite de execução. "Não vai ao PACS" não é categoria — é a ausência
    /// do satélite <c>ExameImagem</c> (ADR-0021).
    /// </summary>
    public static bool CriaSateliteImagem(CategoriaSolicitacao categoria) =>
        categoria == CategoriaSolicitacao.Imagem;

    private static string SoDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    private static string SemAcento(string s) =>
        new string([.. s.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
}
