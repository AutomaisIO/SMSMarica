using System.Globalization;
using System.Text;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Solicitacoes;

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
    /// Se o pedido ganha satélite de execução (<c>ExameImagem</c>: accession, Study, worklist).
    /// "Não vai ao PACS" não é categoria — é a ausência do satélite (ADR-0021).
    ///
    /// <para><b>A categoria diz o que o exame É; a configuração diz COMO ele é executado</b>
    /// (adendo de 20/10/2026 ao ADR-0021). Imagem tem satélite sempre — a invariante de 05/10
    /// continua. Fora da imagem, o pedido só ganha satélite quando as DUAS coisas valem: o TIPO de
    /// exame tem modalidade DICOM <b>e</b> a unidade executante o tem no escopo com o envio à
    /// worklist ligado (ADR-0056) — é a unidade que está integrada a um aparelho. É o caso do Holter
    /// (ECG) e do MAPA (OT) no CDT: seguem Gráfico/funcional, vão à worklist da cardiologia e recebem
    /// o laudo em PDF pelo PACS, casado pelo accession/Study.</para>
    ///
    /// <para>Ficam sem: audiometria, EEG, ECG… (tipo sem modalidade); e a ressonância gravada como
    /// "Outro" numa clínica com que não há integração (DIMAGEM — medido em 20/10/2026: só a
    /// modalidade bastava, e o reparo deu exame a 178 delas).</para>
    /// </summary>
    /// <param name="modalidadeDoTipo">Modalidade do tipo de exame do pedido; null quando não há tipo.</param>
    /// <param name="unidadeEnviaParaWorklist">A unidade executante tem o tipo no escopo, ativo, com o
    /// envio à worklist ligado.</param>
    public static bool CriaSatelite(
        CategoriaSolicitacao categoria, ModalidadeDicom? modalidadeDoTipo, bool unidadeEnviaParaWorklist) =>
        categoria == CategoriaSolicitacao.Imagem
        || unidadeEnviaParaWorklist
           && modalidadeDoTipo is { } modalidade && modalidade != ModalidadeDicom.Indefinida;

    private static string SoDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    private static string SemAcento(string s) =>
        new string([.. s.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
}
