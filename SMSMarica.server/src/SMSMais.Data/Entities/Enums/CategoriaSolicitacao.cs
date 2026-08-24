namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Tipo clínico de uma <c>Solicitacao</c> (regulação). Valor POSITIVO derivado do subgrupo SIGTAP
/// na importação — NÃO é "vai ao PACS ou não" (isso é expresso pela presença do satélite
/// <c>ExameImagem</c> + <c>TipoExame.EnviarParaWorklist</c>). Ver ADR-0021.
///
/// A categoria roteia a criação do satélite de execução e a UI; ela não é o artefato de resultado.
/// Mapa subgrupo → categoria (no importador):
///   0301/0302 → Consulta · 0204/0205/0206 → Imagem · 0202/0203 → Laboratorio ·
///   0211 → GraficoFuncional · 0209 → Endoscopia · 04.. → Cirurgia · resto → Outro.
/// </summary>
public enum CategoriaSolicitacao
{
    /// <summary>Consulta / atendimento ambulatorial (0301/0302). Sem satélite por ora.</summary>
    Consulta = 1,

    /// <summary>Exame de imagem que produz estudo DICOM/PACS (0204 RX/mamo, 0205 US, 0206 TC/RM).
    /// Única categoria que cria o satélite <see cref="Entities.ExameImagem"/>.</summary>
    Imagem = 2,

    /// <summary>Exame laboratorial (0202 lab clínico, 0203 patologia). Satélite futuro.</summary>
    Laboratorio = 3,

    /// <summary>Método gráfico/funcional (0211: ECG, EEG, audiometria, espirometria, Holter/MAPA,
    /// fundoscopia). Não vai ao PACS; resultado por anexo digitalizado.</summary>
    GraficoFuncional = 4,

    /// <summary>Endoscopia (0209). Não modelada como imagem PACS por ora.</summary>
    Endoscopia = 5,

    /// <summary>Procedimento cirúrgico (04..).</summary>
    Cirurgia = 6,

    /// <summary>Subgrupo desconhecido/ambíguo (09.. OCI, 07.., 03xx clínicos, etc.). Só espinha,
    /// nunca inventa satélite.</summary>
    Outro = 99,
}
