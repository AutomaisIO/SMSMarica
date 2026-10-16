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

    /// <summary>
    /// Em que sequência(s) do item de worklist vai o código do procedimento (código interno do
    /// SISREG, em (0008,0100)) — ADR-0074. <see cref="DestinoCodigoProcedimento.Nenhum"/> (padrão)
    /// = não envia, e o item sai exatamente como antes. Liga-se aparelho a aparelho, quando ele está
    /// cadastrado com esses códigos (ex.: o raio-X do CDT escolhe o protocolo pelo código).
    /// </summary>
    public DestinoCodigoProcedimento CodigoProcedimentoDestinos { get; set; } = DestinoCodigoProcedimento.Nenhum;

    /// <summary>Esquema do código, em (0008,0102). Null = <see cref="CodigoProcedimentoEsquemaPadrao"/>.
    /// Até 16 caracteres (VR <c>SH</c>).</summary>
    public string? CodigoProcedimentoEsquema { get; set; }

    /// <summary>Esquema privado (os privados começam com "99", PS3.16) para o código interno do
    /// SISREG.</summary>
    public const string CodigoProcedimentoEsquemaPadrao = "99SMS";

    // ---- Campos opcionais do item de worklist (ADR-0074, adendo de 16/10/2026) ----
    // Tags gravadas como 8 dígitos hexadecimais, sem vírgula ("00080090"). Cada aparelho lê o seu
    // campo, e quem descobre qual é o técnico, testando — por isso a tag é escolhida no cadastro.

    /// <summary>
    /// Tag onde vai o código do procedimento quando o aparelho NÃO o lê numa sequência de código.
    /// Null = o padrão: (0008,0100) dentro das sequências de <see cref="CodigoProcedimentoDestinos"/>.
    /// Preenchida, o código vai como texto simples nesta tag e as sequências não são usadas.
    /// </summary>
    public string? CodigoProcedimentoTag { get; set; }

    /// <summary>Tag do nome do médico solicitante. Null = não envia (padrão). O usual é (0008,0090)
    /// Referring Physician's Name — é a que o CardioNet pede.</summary>
    public string? MedicoSolicitanteTag { get; set; }

    /// <summary>Tag do nome da unidade (a "clínica" de alguns consoles). Null = não envia (padrão).</summary>
    public string? UnidadeTag { get; set; }

    /// <summary>De qual unidade do pedido sai o nome de <see cref="UnidadeTag"/>.</summary>
    public OrigemUnidadeWorklist UnidadeOrigem { get; set; } = OrigemUnidadeWorklist.Executante;

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
