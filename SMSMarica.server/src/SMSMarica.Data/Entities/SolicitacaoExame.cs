using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Pedido de exame de imagem que origina worklist DICOM no dcm4chee (via
/// UPS-RS). Aderente ao FHIR R4 ServiceRequest. Append-only no sentido de
/// história: o ciclo de vida é registrado via Status + timestamps específicos
/// (DataAgendada, IniciadoEm, RealizadoEm, CanceladoEm). Auditoria conforme
/// ADR-0006.
/// </summary>
public class SolicitacaoExame
{
    public Guid Id { get; set; }

    // ---- Identificação DICOM (chaves que viajam com o exame) ----

    /// <summary>"SMS{aaaa}{seq6}" — chave do pedido que aparece no equipamento e no Study DICOM (tag 0008,0050).</summary>
    public string AccessionNumber { get; set; } = string.Empty;

    /// <summary>Pre-gerado pela SMSMarica (formato 2.25.{guid-numerico}) — vai no Study e no Workitem.</summary>
    public string StudyInstanceUID { get; set; } = string.Empty;

    /// <summary>UID do workitem dentro do dcm4chee (retornado pelo POST UPS-RS). Permite cancelar/atualizar depois.</summary>
    public string? WorklistItemUid { get; set; }

    // ---- Vínculos ----

    /// <summary>Aponta para fhir.patient (hub FHIR) — sem FK/navegação local.</summary>
    public Guid PacienteId { get; set; }

    public Guid TipoExameId { get; set; }
    public TipoExame? TipoExame { get; set; }

    /// <summary>Unidade EXECUTANTE (onde o exame é realizado).</summary>
    public Guid UnidadeId { get; set; }
    public Unidade? Unidade { get; set; }

    /// <summary>Unidade SOLICITANTE (que pediu o exame) — opcional. Distinta da executante.
    /// Preenchida na importação SISREG (unidade solicitante da ficha) ou manualmente na tela.</summary>
    public Guid? UnidadeSolicitanteId { get; set; }
    public Unidade? UnidadeSolicitante { get; set; }

    // ---- Solicitante (snapshot do profissional que pediu: médico/CRM ou enfermeiro/COREN) ----

    /// <summary>FK opcional para o profissional interno cadastrado. Null quando externo.</summary>
    public Guid? SolicitanteUsuarioId { get; set; }
    public Usuario? SolicitanteUsuario { get; set; }

    public string SolicitanteNome { get; set; } = string.Empty;

    /// <summary>Número do registro no conselho (nº do CRM do médico ou do COREN do enfermeiro).
    /// Nome genérico de propósito — o tipo de conselho fica em <see cref="SolicitanteConselho"/>.</summary>
    public string SolicitanteNumConselho { get; set; } = string.Empty;

    /// <summary>UF do conselho (CRM/COREN).</summary>
    public string SolicitanteUfConselho { get; set; } = string.Empty;

    /// <summary>CPF do médico solicitante (só dígitos). Uso INTERNO — âncora da importação SISREG
    /// e base para resolver CPF→CRM. NÃO é exposto em DTO/laudo/PDF (o que se exibe é o CRM/COREN).
    /// O SISREG entrega o CPF na ficha; o CRM vem vazio e é derivado depois.</summary>
    public string? SolicitanteCpf { get; set; }

    /// <summary>Conselho do solicitante: "CRM" (médico) ou "COREN" (enfermeiro). Default "CRM" (legado).</summary>
    public string SolicitanteConselho { get; set; } = "CRM";

    // ---- Regulação ----

    /// <summary>
    /// Código da solicitação (regulação). Substitui o antigo NumeroRegulacaoSus. Regra de
    /// preenchimento (validada na entrada): número a partir de 9999, ou o sentinela
    /// <c>0000</c> para exames feitos extra-SUS em caráter emergencial.
    /// </summary>
    public string? CodigoSolicitacao { get; set; }

    /// <summary>Chave de confirmação da solicitação. Mesma régua do código (≥ 9999 ou <c>0000</c>).</summary>
    public string? ChaveConfirmacao { get; set; }

    public string? Justificativa { get; set; }

    // ---- Fluxo ----

    public StatusSolicitacaoExame Status { get; set; } = StatusSolicitacaoExame.Solicitada;
    public PrioridadeSolicitacao Prioridade { get; set; } = PrioridadeSolicitacao.Eletiva;
    public string? Observacoes { get; set; }

    public DateTime? DataAgendada { get; set; }
    public DateTime? IniciadoEm { get; set; }

    /// <summary>
    /// Momento em que o servidor DETECTOU o exame no PACS (auditoria — hora do servidor,
    /// UTC). NÃO é a data em que o exame foi feito; para isso use <see cref="DataEstudo"/>.
    /// </summary>
    public DateTime? RealizadoEm { get; set; }

    /// <summary>
    /// Data/hora REAL de execução do exame, lida do DICOM (StudyDate 0008,0020 +
    /// StudyTime 0008,0030) — a FONTE DA VERDADE da data do exame/laudo. É wall-clock
    /// local do equipamento (Kind=Unspecified), por isso mapeada como
    /// <c>timestamp without time zone</c>. Null quando o PACS não respondeu ou o estudo
    /// não trouxe a tag (a exibição faz fallback para RealizadoEm/CriadoEm).
    /// </summary>
    public DateTime? DataEstudo { get; set; }

    /// <summary>Quando a integração UPS-RS falha, guardamos o motivo (e o admin pode reenviar manualmente).</summary>
    public string? ErroIntegracaoPacs { get; set; }

    // ---- Retry resiliente (worker EnviadorWorklistService) ----

    /// <summary>Contador de tentativas de envio/confirmação no PACS.</summary>
    public int TentativasEnvio { get; set; }

    /// <summary>Quando foi a última tentativa (envio ou GET de confirmação).</summary>
    public DateTime? UltimaTentativaEm { get; set; }

    /// <summary>Quando o worker deve tentar de novo. Preenchido ao criar (= now)
    /// para o worker pegar imediatamente; em cada falha pula com backoff
    /// exponencial. Null = sem tentativa agendada (estados terminais).</summary>
    public DateTime? ProximaTentativaEm { get; set; }

    // ---- Cancelamento ----

    public DateTime? CanceladoEm { get; set; }
    public Guid? CanceladoPorUsuarioId { get; set; }
    public string? MotivoCancelamento { get; set; }

    // ---- Auditoria ADR-0006 ----

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }
}
