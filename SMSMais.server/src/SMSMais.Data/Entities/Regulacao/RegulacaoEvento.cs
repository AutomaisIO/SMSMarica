using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// Um fato na vida da solicitação — <b>append-only</b> (ADR-0052).
///
/// <para><b>Por que não basta o <c>atualizado_em</c> da solicitação:</b> a pergunta que a regulação
/// precisa responder não é "quando mudou", é "quem mudou o quê, com qual senha e por quê". Uma
/// solicitação que passou por três agentes e voltou duas vezes para a unidade tem uma história, e
/// ela não cabe em colunas de última alteração.</para>
///
/// <para><b>Nada aqui é editado nem apagado.</b> Corrigir um evento errado significa gravar outro
/// — é o que faz a trilha valer como prova.</para>
///
/// <para>Não substitui <c>registro_auditoria</c>: lá vai só o que é de segurança (uso de
/// credencial pessoal). Aqui é a história do caso, que o solicitante também enxerga.</para>
/// </summary>
public sealed class RegulacaoEvento
{
    public Guid Id { get; set; }

    public Guid SolicitacaoId { get; set; }
    public RegulacaoSolicitacao? Solicitacao { get; set; }

    public TipoEventoRegulacao Tipo { get; set; }

    /// <summary>Nulos quando o evento não mudou o estado (um anexo, uma resposta de regra).</summary>
    public StatusRegulacao? StatusAnterior { get; set; }
    public StatusRegulacao? StatusNovo { get; set; }

    /// <summary><c>null</c> = sistema (varredura, importação, job).</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>
    /// Nome no momento do evento. Redundante de propósito: usuário é renomeado e desativado, e a
    /// linha do tempo tem de continuar legível anos depois.
    /// </summary>
    public string? UsuarioNome { get; set; }

    public PapelEventoRegulacao Papel { get; set; }

    /// <summary>Unidade ativa de quem agiu — o mesmo usuário age por unidades diferentes.</summary>
    public Guid? UnidadeAtivaId { get; set; }

    public string? Ip { get; set; }

    /// <summary>`jti` do token: liga os eventos de uma mesma sessão.</summary>
    public string? SessaoId { get; set; }

    /// <summary>O que mudou: <c>{campo: {de, para}}</c>. É isto que torna o "Ajuste" do agente auditável.</summary>
    public string? DiffJson { get; set; }

    /// <summary>Livre por tipo de evento: motivo, número externo, credencial usada, login.</summary>
    public string? DetalheJson { get; set; }

    public DateTime CriadoEm { get; set; }
}

/// <summary>
/// Se a solicitação pode ir para cada sistema de regulação, e por quê — avaliado pelas regras de
/// elegibilidade (plano 03, incremento 4).
///
/// <para><b>Uma linha por sistema, e não uma coluna na solicitação</b>, porque o mesmo procedimento
/// pode estar bloqueado no SER e livre no SERNIT: é a "ressalva de destino". Guardar só um
/// veredito perderia justamente a informação que decide para onde o agente manda.</para>
/// </summary>
public sealed class RegulacaoSolicitacaoDestino
{
    public Guid Id { get; set; }

    public Guid SolicitacaoId { get; set; }
    public RegulacaoSolicitacao? Solicitacao { get; set; }

    public SistemaRegulacao Sistema { get; set; }

    public SituacaoDestinoRegulacao Situacao { get; set; }

    /// <summary>Texto que o agente lê para entender o bloqueio — vem da regra que barrou.</summary>
    public string? Motivo { get; set; }

    public DateTime AvaliadoEm { get; set; }
}
