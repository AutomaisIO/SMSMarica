namespace SMSMais.Data.Entities;

/// <summary>
/// Snapshot imutável (append-only) das anotações de um estudo PACS feitas no
/// visualizador Cornerstone. Cada save do usuário gera uma nova linha, com
/// versão sequencial dentro do mesmo <see cref="StudyInstanceUID"/>.
/// </summary>
public class EstudoAnotacao
{
    public Guid Id { get; set; }

    /// <summary>UID do estudo DICOM (identifica o exame no dcm4chee).</summary>
    public string StudyInstanceUID { get; set; } = string.Empty;

    /// <summary>Versão sequencial dentro do estudo, começando em 1.</summary>
    public int Versao { get; set; }

    /// <summary>
    /// Estado bruto exportado de <c>annotationManager.state.getAllAnnotations()</c>
    /// (Cornerstone Tools). Armazenado como jsonb pra permitir inspeção futura
    /// sem precisar desserializar no front.
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>Usuário (médico) que gravou esta versão.</summary>
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Comentário opcional do save ("revisão pós-laudo", "marcação inicial", etc.).</summary>
    public string? Comentario { get; set; }
}
