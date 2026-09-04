using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Entities.Robo;

/// <summary>
/// Um <b>item de treinamento</b>: a crítica que um atendente escreveu sobre uma resposta do robô,
/// mais todo o ciclo que ela dispara — observação do humano, análise adversarial do agente
/// treinador, alterações aplicadas no material do assunto, pendências que travaram o caminho e as
/// simulações de verificação.
///
/// Diferente de <see cref="RoboErroResposta"/>, que é só a <i>captura</i> do clique na bolha (e
/// continua existindo, com os dados já em produção): o item é o <i>processo</i>. Um item nasce de
/// uma captura, mas também pode nascer sozinho — o operador querendo ensinar algo sem ter uma
/// mensagem específica na mão.
/// </summary>
public class RoboTreinamentoItem
{
    public Guid Id { get; set; }

    /// <summary>Captura que originou o item (o clique no ícone da bolha). Nulo quando o item foi
    /// aberto direto na tela de treinamento.</summary>
    public Guid? RoboErroRespostaId { get; set; }

    public Guid? ConversaId { get; set; }

    /// <summary>Mensagem do robô criticada.</summary>
    public Guid? MensagemWhatsAppId { get; set; }

    /// <summary>Assunto engajado quando a resposta saiu — é o alvo natural da correção.</summary>
    public Guid? RoboAssuntoId { get; set; }

    /// <summary>Snapshot do que o robô disse (a mensagem pode ser apagada depois; a crítica não
    /// pode perder o objeto).</summary>
    public string? Trecho { get; set; }

    /// <summary>Recorte do diálogo em volta da mensagem criticada (JSON: papel + texto), para o
    /// agente entender o que veio antes. Congelado no momento da crítica.</summary>
    public string? ContextoJson { get; set; }

    /// <summary>A crítica/correção que o atendente escreveu no modal.</summary>
    public string Critica { get; set; } = string.Empty;

    /// <summary>O que o humano acrescentou ao mandar treinar ("mais alguma observação?").</summary>
    public string? Observacao { get; set; }

    public StatusTreinamentoRobo Status { get; set; } = StatusTreinamentoRobo.Aberto;

    /// <summary>Parecer final do agente, em texto, para o humano ler na tela.</summary>
    public string? Analise { get; set; }

    /// <summary>Rastro completo da análise (proposta, ataques dos adversários, veredito do juiz)
    /// em JSON — é o que permite auditar por que uma regra entrou.</summary>
    public string? AnaliseJson { get; set; }

    /// <summary>Modelo usado na análise (ex.: <c>claude-fable-5-1</c>).</summary>
    public string? Modelo { get; set; }

    public long TokensEntrada { get; set; }
    public long TokensSaida { get; set; }
    public decimal CustoUsd { get; set; }

    public DateTime? AnalisadoEm { get; set; }

    /// <summary>Quantas vezes o worker já tentou analisar. Um item que derruba a análise ficaria
    /// em laço eterno na fila; com teto, ele para em <see cref="StatusTreinamentoRobo.Falhou"/>.</summary>
    public int TentativasAnalise { get; set; }

    /// <summary>Mensagem do erro quando <see cref="StatusTreinamentoRobo.Falhou"/>.</summary>
    public string? ErroMensagem { get; set; }

    /// <summary>Token de concorrência (xmin do Postgres) — dois operadores podem abrir o mesmo item.</summary>
    public uint RowVersion { get; set; }

    public ICollection<RoboTreinamentoPendencia> Pendencias { get; set; } = [];
    public ICollection<RoboTreinamentoAlteracao> Alteracoes { get; set; } = [];
    public ICollection<RoboTreinamentoSimulacao> Simulacoes { get; set; } = [];

    public Conversa? Conversa { get; set; }
    public RoboAssunto? RoboAssunto { get; set; }
    public MensagemWhatsApp? Mensagem { get; set; }
    public RoboErroResposta? RoboErroResposta { get; set; }

    // Auditoria ADR-0006
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}

/// <summary>
/// Uma dúvida que travou a análise. O agente não decide regra de negócio nem autoriza
/// desenvolvimento: quando esbarra numa dessas, abre a pendência aqui e para. Abrir o item na tela
/// obriga a responder antes de o ciclo prosseguir.
/// </summary>
public class RoboTreinamentoPendencia
{
    public Guid Id { get; set; }

    public Guid RoboTreinamentoItemId { get; set; }

    public TipoPendenciaTreinamento Tipo { get; set; }

    /// <summary>A pergunta objetiva ao humano.</summary>
    public string Pergunta { get; set; } = string.Empty;

    /// <summary>Por que o agente precisa disso — o conflito ou a limitação que ele encontrou.</summary>
    public string? Contexto { get; set; }

    /// <summary>Opções sugeridas pelo agente (JSON: array de strings). O humano pode escrever outra.</summary>
    public string? OpcoesJson { get; set; }

    public StatusPendenciaTreinamento Status { get; set; } = StatusPendenciaTreinamento.Aberta;

    /// <summary>Resposta do humano (a decisão de negócio, ou a justificativa da dispensa).</summary>
    public string? Resposta { get; set; }

    /// <summary>Só para <see cref="TipoPendenciaTreinamento.AlteracaoCodigo"/>: o humano autorizou
    /// que isso vire trabalho de desenvolvimento. Nulo = ainda não decidiu.</summary>
    public bool? Autorizado { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? RespondidoEm { get; set; }
    public Guid? RespondidoPor { get; set; }

    public RoboTreinamentoItem? Item { get; set; }
}

/// <summary>
/// O que o agente efetivamente mudou no material do robô. Guarda o valor anterior em JSON — é o
/// que permite o <b>desfazer</b>, que é a contrapartida de deixar o agente aplicar sozinho.
/// </summary>
public class RoboTreinamentoAlteracao
{
    public Guid Id { get; set; }

    public Guid RoboTreinamentoItemId { get; set; }

    public AlvoAlteracaoTreinamento Alvo { get; set; }

    public OperacaoAlteracaoTreinamento Operacao { get; set; }

    /// <summary>Assunto cujo material foi alterado.</summary>
    public Guid RoboAssuntoId { get; set; }

    /// <summary>Id da linha alterada (treino ou condição). Preenchido também na criação.</summary>
    public Guid AlvoId { get; set; }

    /// <summary>Estado anterior da linha em JSON. Nulo quando a operação foi criação.</summary>
    public string? ValorAnteriorJson { get; set; }

    /// <summary>Estado resultante em JSON.</summary>
    public string? ValorNovoJson { get; set; }

    /// <summary>Por que o agente fez essa mudança (entra no histórico da tela).</summary>
    public string? Justificativa { get; set; }

    public DateTime AplicadoEm { get; set; }

    /// <summary>Preenchido quando um humano desfez a alteração.</summary>
    public DateTime? DesfeitoEm { get; set; }
    public Guid? DesfeitoPor { get; set; }

    public RoboTreinamentoItem? Item { get; set; }
    public RoboAssunto? RoboAssunto { get; set; }
}

/// <summary>
/// Ensaio do caso criticado contra o <b>modelo treinado atual</b>, com um juiz dizendo se a
/// crítica foi de fato atendida. Roda automaticamente depois de aplicar as alterações e pode ser
/// disparado à mão a qualquer momento (inclusive em item já concluído, para checar regressão).
/// </summary>
public class RoboTreinamentoSimulacao
{
    public Guid Id { get; set; }

    public Guid RoboTreinamentoItemId { get; set; }

    /// <summary>Mensagem do cidadão usada no ensaio.</summary>
    public string Mensagem { get; set; } = string.Empty;

    /// <summary>Histórico anterior usado no ensaio (JSON: papel + texto).</summary>
    public string? HistoricoJson { get; set; }

    /// <summary>Assunto que a classificação escolheu no ensaio (pode diferir do original — é assim
    /// que se enxerga uma correção de roteamento).</summary>
    public string? AssuntoNome { get; set; }

    /// <summary>O que o robô responderia hoje.</summary>
    public string? Resposta { get; set; }

    /// <summary>Comandos que ele chamaria, em JSON (os de escrita não executam).</summary>
    public string? ChamadasJson { get; set; }

    public VereditoSimulacaoTreinamento? Veredito { get; set; }

    /// <summary>Parecer do juiz sobre a resposta, à luz da crítica original.</summary>
    public string? Analise { get; set; }

    public long TokensEntrada { get; set; }
    public long TokensSaida { get; set; }
    public decimal CustoUsd { get; set; }
    public long DuracaoMs { get; set; }

    /// <summary>Disparada pelo ciclo automático após aplicar alterações (vs. botão do humano).</summary>
    public bool Automatica { get; set; }

    public string? ErroMensagem { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }

    public RoboTreinamentoItem? Item { get; set; }
}
