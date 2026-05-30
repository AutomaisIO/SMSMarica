using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Fhir;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Laudo radiológico emitido a partir de um estudo PACS (dcm4chee).
/// Append-only: cada finalização é definitiva; correções entram como nova
/// versão (<see cref="Versao"/>+1, <see cref="LaudoAnteriorId"/> apontando
/// para a versão anterior). Soft-delete só permitido para rascunhos.
/// </summary>
public class Laudo
{
    public Guid Id { get; set; }

    /// <summary>UID do estudo DICOM (identifica o exame no dcm4chee, sem FK local).</summary>
    public string StudyInstanceUID { get; set; } = string.Empty;

    /// <summary>Sequencial dentro do mesmo <see cref="StudyInstanceUID"/>, começando em 1.</summary>
    public int Versao { get; set; }

    /// <summary>Versão anterior na cadeia (null para a primeira).</summary>
    public Guid? LaudoAnteriorId { get; set; }
    public Laudo? LaudoAnterior { get; set; }

    /// <summary>Paciente vinculado (nullable — mamógrafo pode mandar estudo sem paciente cadastrado).</summary>
    public Guid? PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Practitioner autor (fhir.practitioner). Obrigatório. Após Fatia 3 substitui o antigo Medico.</summary>
    public Guid PractitionerId { get; set; }
    public Practitioner? Practitioner { get; set; }

    /// <summary>Template usado como ponto de partida (snapshot do conteúdo). Nullable porque o template pode ter sido excluído.</summary>
    public Guid? LaudoTemplateId { get; set; }
    public LaudoTemplate? LaudoTemplate { get; set; }

    public string Titulo { get; set; } = string.Empty;

    /// <summary>Documento ProseMirror/TipTap nativo (re-editável).</summary>
    public string ConteudoJson { get; set; } = "{}";

    /// <summary>HTML sanitizado — usado para gerar o PDF.</summary>
    public string ConteudoHtml { get; set; } = string.Empty;

    public StatusLaudo Status { get; set; } = StatusLaudo.Rascunho;

    /// <summary>Congelado ao finalizar (CRM pode mudar de UF; profissional pode sair).</summary>
    public string? PractitionerNomeSnapshot { get; set; }
    public string? PractitionerCrmSnapshot { get; set; }
    public string? PractitionerUfCrmSnapshot { get; set; }
    public string? PractitionerRqeSnapshot { get; set; }

    public DateTime? FinalizadoEm { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    // Soft-delete (CFM: laudo médico não se apaga fisicamente).
    public bool Excluido { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPorUsuarioId { get; set; }

    /// <summary>Concorrência otimista via PG xmin.</summary>
    public uint RowVersion { get; set; }
}
