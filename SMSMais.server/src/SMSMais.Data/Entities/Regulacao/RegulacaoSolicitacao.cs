using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Solicitação aberta dentro do SMSMais, antes e depois de ir para o sistema de regulação
/// (ADR-0052).
///
/// <para><b>Não é a <see cref="Solicitacao"/> de exame, nem o espelho `ser_solicitacao`.</b> Esta
/// é a solicitação do ponto de vista de quem pede: nasce na unidade, passa pela pré-regulação e
/// só então vira um número em sistema de terceiro. Os espelhos continuam sendo o retrato do que
/// existe lá fora, e se ligam a esta por FK quando a varredura casa o número externo.</para>
///
/// <para><b>Depois do envio, quem manda é o número externo.</b> O <see cref="NumeroLocal"/> serve
/// para a ponta conversar sobre a solicitação antes de existir número de verdade.</para>
/// </summary>
public sealed class RegulacaoSolicitacao
{
    public Guid Id { get; set; }

    /// <summary>Número sequencial legível, gerado pelo banco. Vira rastro depois do número externo.</summary>
    public long NumeroLocal { get; set; }

    public FluxoRegulacao Fluxo { get; set; }

    /// <summary>Unidade que abriu. É por ela que o escopo da fila filtra.</summary>
    public Guid UnidadeSolicitanteId { get; set; }
    public Unidade? UnidadeSolicitante { get; set; }

    /// <summary>
    /// Só no NAR: a unidade em cujo nome a solicitação entra no SISREG. É a credencial dela que
    /// o agente usa para incluir (D-9).
    /// </summary>
    public Guid? UnidadeEmNomeDeId { get; set; }
    public Unidade? UnidadeEmNomeDe { get; set; }

    public Guid CriadoPorUsuarioId { get; set; }

    /// <summary>
    /// Paciente em <c>fhir.patient</c>. <b>Sem FK física</b>: FK cross-schema só pode ir de
    /// `smsmarica` para `fhir` e o EF não modela isso bem aqui — a integridade é do service.
    /// </summary>
    public Guid PacienteId { get; set; }

    /// <summary>
    /// Cópia do identificador e do nome no momento da abertura. Redundante de propósito: o
    /// cadastro muda, e a auditoria precisa saber para quem a solicitação foi feita naquele dia.
    /// </summary>
    public string? PacienteCpf { get; set; }
    public string? PacienteCns { get; set; }
    public string PacienteNome { get; set; } = string.Empty;

    public Guid ProcedimentoId { get; set; }
    public RegulacaoProcedimento? Procedimento { get; set; }

    /// <summary>Nulo até o destino ser decidido; no NAR é sempre SISREG.</summary>
    public SistemaRegulacao? SistemaDestino { get; set; }

    /// <summary>
    /// Respostas do formulário: <c>{canonico, sisreg, ser, sernit}</c>. JSON porque o conjunto de
    /// campos muda por procedimento e por sistema — colunas fixas exigiriam migration a cada
    /// mexida da SES numa especialidade.
    /// </summary>
    public string FormularioJson { get; set; } = "{}";

    /// <summary>Versão do formulário usada, para reler a solicitação como ela foi preenchida.</summary>
    public Guid? FormularioVersaoId { get; set; }
    public RegulacaoFormularioVersao? FormularioVersao { get; set; }

    public StatusRegulacao Status { get; set; } = StatusRegulacao.Rascunho;
    public string? StatusMotivo { get; set; }

    public Guid? AgenteResponsavelId { get; set; }

    /// <summary>Código gerado pelo SISREG/SER/SERNIT. A partir dele, é ele que identifica.</summary>
    public string? NumeroExterno { get; set; }

    public DateTime? EnviadoEm { get; set; }
    public Guid? EnviadoPorUsuarioId { get; set; }

    /// <summary>Qual credencial foi usada no envio — a trilha de "quem assinou" (plano 07).</summary>
    public Guid? CredencialUsadaId { get; set; }
    public string? OperadorExternoLogin { get; set; }

    /// <summary>
    /// Envio assistido: o agente incluiu no sistema pela tela dele e digitou o número aqui. É o
    /// caminho do incremento 3, e continua valendo depois — não é escrita externa do robô.
    /// </summary>
    public bool EnvioAssistido { get; set; }

    /// <summary>FKs para os espelhos, preenchidas pela conciliação por número externo (plano 05).</summary>
    public Guid? SolicitacaoId { get; set; }
    public Guid? SerSolicitacaoId { get; set; }
    public Guid? SernitSolicitacaoId { get; set; }

    /// <summary>Fim da janela de edição no SISREG, quando existir (a confirmar no spike b).</summary>
    public DateTime? SisregEditavelAte { get; set; }

    /// <summary>Rascunho `ser_*`/`sernit_*` de origem, quando a solicitação veio da migração.</summary>
    public Guid? OrigemLegadoId { get; set; }

    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    /// <summary>
    /// `xmin`. É o que faz o "assumir" da fila ser atômico: dois agentes clicando ao mesmo tempo,
    /// um ganha e o outro recebe conflito em vez de os dois acharem que assumiram.
    /// </summary>
    public uint RowVersion { get; set; }

    public ICollection<RegulacaoSolicitacaoExigencia> Exigencias { get; set; } = [];

    /// <summary>A história do caso, append-only (tarefa 3.1).</summary>
    public ICollection<RegulacaoEvento> Eventos { get; set; } = [];

    /// <summary>Veredito de elegibilidade por sistema de destino.</summary>
    public ICollection<RegulacaoSolicitacaoDestino> Destinos { get; set; } = [];
}
