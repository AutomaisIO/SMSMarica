using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame;

/// <summary>
/// Roteamento determinístico da natureza clínica pelo SUBGRUPO SIGTAP (4 primeiros dígitos) —
/// não adivinha procedimento. Ver ADR-0021. Função pura, sem banco: mesmo critério para o
/// importador do SISREG e para o Processo Regulatório.
/// </summary>
public static class CategoriaSigtap
{
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
    /// Se a categoria cria satélite de execução. "Não vai ao PACS" não é categoria — é a ausência
    /// do satélite <c>ExameImagem</c> (ADR-0021).
    /// </summary>
    public static bool CriaSateliteImagem(CategoriaSolicitacao categoria) =>
        categoria == CategoriaSolicitacao.Imagem;

    private static string SoDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);
}
