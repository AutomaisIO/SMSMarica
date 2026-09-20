using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Ouvidoria;

/// <summary>
/// Manifestação de ouvidoria (ADR-0060): solicitação, reclamação, denúncia, sugestão, elogio
/// ou pedido de informação registrado pelo cidadão ou pela ouvidoria. Uma linha por protocolo.
/// O histórico do que aconteceu fica em <see cref="Eventos"/> (append-only); aqui ficam a
/// classificação, o contexto, o manifestante e os relógios de prazo.
/// </summary>
public sealed class OuvidoriaManifestacao
{
    public Guid Id { get; set; }

    /// <summary>
    /// Protocolo público no formato <c>AAAA-NNNNNN</c> (ano + sequência <c>ouvidoria_protocolo_seq</c>).
    /// É o que o cidadão guarda e usa para acompanhar.
    /// </summary>
    public string Protocolo { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 do código de acesso de 8 caracteres entregue ao cidadão uma única vez no registro.
    /// O código em si não é guardado. <b>Nulo quando anônima</b>: sem contato não há acompanhamento.
    /// </summary>
    public string? CodigoAcessoHash { get; set; }

    // ---- Classificação ----

    public OuvidoriaTipo Tipo { get; set; }
    public OuvidoriaIdentificacao Identificacao { get; set; }
    public OuvidoriaCanal Canal { get; set; }
    public OuvidoriaOrigem Origem { get; set; }
    public OuvidoriaPrioridade Prioridade { get; set; } = OuvidoriaPrioridade.Normal;
    public OuvidoriaStatus Status { get; set; } = OuvidoriaStatus.Registrada;

    /// <summary>Assunto de 1º nível (Manual MS 2014). FK para <c>ouvidoria_assunto</c>.</summary>
    public Guid? AssuntoId { get; set; }

    /// <summary>Subassunto (2º nível), filho de <see cref="AssuntoId"/>. FK para <c>ouvidoria_assunto</c>.</summary>
    public Guid? SubassuntoId { get; set; }

    /// <summary>Resumo curto para a fila; preenchido na triagem.</summary>
    public string? Resumo { get; set; }

    /// <summary>Texto integral do cidadão. Nunca é editado — a correção vira evento.</summary>
    public string Teor { get; set; } = string.Empty;

    /// <summary>
    /// Versão do teor sem nada que identifique o manifestante. Em denúncia, é <b>isto</b> que vai
    /// à unidade apuratória — o ponto de resposta nunca vê <see cref="Teor"/>. Editado só por quem
    /// tem <c>OuvidoriaSigilo</c>; obrigatório para encaminhar denúncia.
    /// </summary>
    public string? TeorPseudonimizado { get; set; }

    // ---- Contexto ----

    /// <summary>Unidade de saúde a que a manifestação se refere (não é escopo de acesso: a ouvidoria é transversal).</summary>
    public Guid? UnidadeId { get; set; }

    /// <summary>Ponto de resposta atual (unidade, área central ou apuração) para onde foi encaminhada.</summary>
    public Guid? PontoRespostaId { get; set; }

    /// <summary>
    /// Vínculo com um pedido de regulação já existente (D-7). A ouvidoria não abre pedido de
    /// regulação — só aponta para um.
    /// </summary>
    public Guid? RegulacaoSolicitacaoId { get; set; }

    /// <summary>Número da manifestação em outro sistema (Fala.BR, Ouvidoria Geral, 136) quando veio ou foi de lá.</summary>
    public string? ProtocoloExterno { get; set; }

    /// <summary>Nome do sistema externo de <see cref="ProtocoloExterno"/>.</summary>
    public string? SistemaExterno { get; set; }

    public DateOnly? DataFato { get; set; }
    public string? LocalFato { get; set; }

    // ---- Manifestante (nunca vai ao DTO do ponto de resposta; mascarado se sigilosa) ----

    public string? ManifestanteNome { get; set; }
    public string? ManifestanteCpf { get; set; }
    public string? ManifestanteTelefone { get; set; }
    public string? ManifestanteEmail { get; set; }

    /// <summary>Patient FHIR do manifestante quando resolvido (hub autônomo: Guid sem FK física).</summary>
    public Guid? ManifestantePatientId { get; set; }

    // ---- Referido: paciente em favor de quem se manifesta (pode ser o próprio) ----

    public Guid? ReferidoPatientId { get; set; }
    public string? ReferidoNome { get; set; }
    public string? ReferidoCpf { get; set; }
    public string? ReferidoCns { get; set; }

    // ---- Envolvido: profissional/servidor citado ----

    /// <summary>Practitioner FHIR citado, quando identificado (sem FK física).</summary>
    public Guid? EnvolvidoPractitionerId { get; set; }

    /// <summary>Descrição livre do envolvido quando não há Practitioner ("a recepcionista do turno da noite").</summary>
    public string? EnvolvidoDescricao { get; set; }

    // ---- Relógios (Lei 13.460 art. 16; D-4) ----

    public DateTime RegistradaEm { get; set; }

    /// <summary>Prazo de resposta ao cidadão (30 dias, +30 na prorrogação, + dias suspensos).</summary>
    public DateOnly PrazoRespostaEm { get; set; }

    /// <summary>Quando foi prorrogada. Só uma vez: não nulo = não pode prorrogar de novo.</summary>
    public DateTime? ProrrogadoEm { get; set; }
    public string? ProrrogacaoJustificativa { get; set; }

    /// <summary>Prazo para a área responder à ouvidoria (por prioridade/ponto; menor que o do cidadão).</summary>
    public DateOnly? PrazoAreaEm { get; set; }
    public DateTime? EncaminhadaEm { get; set; }

    public DateTime? ComplementacaoSolicitadaEm { get; set; }

    /// <summary>A complementação só pode ser pedida uma vez (PN CGU 116 art. 27).</summary>
    public bool ComplementacaoUsada { get; set; }

    /// <summary>Início da suspensão do relógio (pedido de complementação em aberto). Nulo = relógio correndo.</summary>
    public DateTime? SuspensaEm { get; set; }

    /// <summary>
    /// Total de dias em que o relógio ficou suspenso aguardando complementação. Somado ao prazo e
    /// descontado de <see cref="DiasAteResposta"/> — a complementação não conta contra a ouvidoria.
    /// </summary>
    public int DiasSuspensos { get; set; }

    public DateTime? RespondidaEm { get; set; }
    public DateTime? ConcluidaEm { get; set; }

    /// <summary>Dias corridos do registro à resposta conclusiva, já sem <see cref="DiasSuspensos"/>. Base do tempo médio.</summary>
    public int? DiasAteResposta { get; set; }

    /// <summary>Dias além de <see cref="PrazoRespostaEm"/> na resposta conclusiva (0 = no prazo).</summary>
    public int? DiasAtraso { get; set; }

    /// <summary>Carimbo da última ação de qualquer natureza; ordena a fila.</summary>
    public DateTime UltimaAtividadeEm { get; set; }

    // ---- Conclusão ----

    public OuvidoriaResolutividade? Resolutividade { get; set; }
    public OuvidoriaSituacaoFinal? SituacaoFinal { get; set; }
    public OuvidoriaMotivoNaoAtendimento? MotivoNaoAtendimento { get; set; }
    public OuvidoriaMotivoArquivamento? MotivoArquivamento { get; set; }

    /// <summary>Texto da resposta conclusiva ao cidadão (cópia do evento <c>RespostaConclusiva</c>, para leitura rápida).</summary>
    public string? RespostaConclusiva { get; set; }

    // ---- Denúncia ----

    /// <summary>
    /// Quando a denúncia foi habilitada (juízo de admissibilidade: autoria, materialidade,
    /// competência — PN CGU 116 art. 33). Denúncia só é encaminhada depois disto.
    /// </summary>
    public DateTime? HabilitadaEm { get; set; }
    public Guid? HabilitadaPor { get; set; }

    // ---- Trabalho ----

    /// <summary>Técnico da ouvidoria responsável pelo acompanhamento (atribuído na triagem; sem FK física, como os carimbos de auditoria).</summary>
    public Guid? ResponsavelId { get; set; }

    // ---- Auditoria ADR-0006 ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>Concorrência otimista (PG xmin).</summary>
    public uint RowVersion { get; set; }

    // ---- Navegações ----
    public OuvidoriaAssunto? Assunto { get; set; }
    public OuvidoriaAssunto? Subassunto { get; set; }
    public Unidade? Unidade { get; set; }
    public OuvidoriaPontoResposta? PontoResposta { get; set; }
    public ICollection<OuvidoriaEvento> Eventos { get; set; } = [];
    public ICollection<OuvidoriaAnexo> Anexos { get; set; } = [];
    public ICollection<OuvidoriaManifestacaoMarcador> Marcadores { get; set; } = [];
}
