using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Equipamento de uma unidade (ex.: mamógrafo, ultrassom, tomógrafo) — recurso
/// agendável para exames de imagem. Infra operacional do <c>smsmarica</c>; canônico
/// FHIR seria Device, mas fica local (ver ADR-0013).
/// </summary>
public class Equipamento
{
    public Guid Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    public ModalidadeDicom ModalidadeDicom { get; set; }

    /// <summary>Identificador DICOM opcional (AE Title / Station Name) — para o worklist do PACS no futuro.</summary>
    public string? IdentificadorDicom { get; set; }

    /// <summary>
    /// Quantos caracteres da descrição do exame este aparelho aguenta no item de worklist
    /// (<c>RequestedProcedureDescription</c> 0032,1060 e <c>SPS Description</c> 0040,0007).
    ///
    /// <para><b>O padrão é 64</b>, que é o limite do VR <c>LO</c> do DICOM. O campo existe porque
    /// <b>um</b> aparelho não aguenta o padrão: o mamógrafo Fuji FDR-3000AWS falha ao montar a
    /// imagem (erro 31027, "obter informações de imagem") com descrições longas, e a worklist de
    /// referência que o console dele aceitava usava ~10 caracteres. Até 09/09/2026 esse limite de
    /// 16 valia para a rede inteira — um remendo de um aparelho cobrando imposto de todos, que
    /// truncava 189 dos 205 tipos de exame no meio da palavra ("RADIOGRAFIA DE T").</para>
    ///
    /// <para>Baixar isto só se um console concreto falhar; subir acima de 64 não adianta, o DICOM
    /// corta.</para>
    /// </summary>
    public int DescricaoMaxCaracteres { get; set; } = DescricaoMaxPadrao;

    /// <summary>Limite do VR <c>LO</c> do DICOM — o teto de qualquer aparelho.</summary>
    public const int DescricaoMaxPadrao = 64;

    /// <summary>Visibilidade no agendamento. Soft-delete via <see cref="ExcluidoEm"/>.</summary>
    public bool Ativo { get; set; } = true;

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }
}
