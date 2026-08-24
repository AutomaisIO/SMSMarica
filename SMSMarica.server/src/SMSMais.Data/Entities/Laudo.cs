using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

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

    /// <summary>Paciente vinculado em fhir.patient (nullable). Sem FK/navegação local.</summary>
    public Guid? PacienteId { get; set; }

    /// <summary>
    /// Nome do paciente como veio no DICOM (0010,0010) do estudo em que o laudo foi
    /// gerado — capturado na criação quando o exame ainda NÃO tem vínculo. É apenas
    /// uma referência TEMPORÁRIA ("de quem parece ser") para não deixar o laudo
    /// órfão sem nenhuma pista; não é paciente confiável. Ao associar o exame a uma
    /// solicitação (<see cref="PacienteId"/> passa a valer) este campo é limpo.
    /// </summary>
    public string? PacienteNomeDicom { get; set; }

    /// <summary>Médico autor (Practitioner em fhir.practitioner). Sem FK local — nome/CRM via snapshots abaixo.</summary>
    public Guid MedicoId { get; set; }

    /// <summary>Template usado como ponto de partida (snapshot do conteúdo). Nullable porque o template pode ter sido excluído.</summary>
    public Guid? LaudoTemplateId { get; set; }
    public LaudoTemplate? LaudoTemplate { get; set; }

    public string Titulo { get; set; } = string.Empty;

    /// <summary>Documento ProseMirror/TipTap nativo (re-editável).</summary>
    public string ConteudoJson { get; set; } = "{}";

    /// <summary>HTML sanitizado — usado para gerar o PDF.</summary>
    public string ConteudoHtml { get; set; } = string.Empty;

    public StatusLaudo Status { get; set; } = StatusLaudo.Rascunho;

    /// <summary>
    /// Categoria BI-RADS FINAL do laudo (ex.: "2", "4B"). É o valor que a
    /// profissional confirmou — pode divergir do sugerido pelo cálculo. Campo
    /// explícito e indexado para busca. Null em laudos sem avaliação BI-RADS.
    /// </summary>
    public string? BiRads { get; set; }

    /// <summary>Categoria sugerida pelo motor a partir das respostas (auditoria do override).</summary>
    public string? BiRadsSugerido { get; set; }

    /// <summary>
    /// Respostas do checklist que originaram o texto/BI-RADS (jsonb). Permite
    /// reabrir o laudo no painel estruturado. Null em laudos de texto livre.
    /// </summary>
    public string? RespostasChecklist { get; set; }

    /// <summary>Congelado ao finalizar (CRM pode mudar de UF; médico pode sair).</summary>
    public string? MedicoNomeSnapshot { get; set; }
    public string? MedicoCrmSnapshot { get; set; }
    public string? MedicoUfCrmSnapshot { get; set; }
    public string? MedicoRqeSnapshot { get; set; }

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
