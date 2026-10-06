namespace SMSMais.Data.Entities.AgenteIa;

/// <summary>
/// Uma mensagem do celular de aviso que vai para o Agente IA (ADR-0068). O webhook só grava esta
/// linha — o relay do Zap espera poucos segundos e um turno do agente leva minutos. Quem leva ao
/// motor, acompanha o turno e devolve o andamento e a resposta pelo WhatsApp é o
/// <c>AgenteWhatsAppWorker</c>.
///
/// <para>Uma por mensagem, na ordem em que chegaram: mensagem que chega com um turno rodando
/// espera aqui e vai depois — não se perde e não atropela o turno em andamento.</para>
/// </summary>
public sealed class AgenteWhatsAppPedido
{
    public Guid Id { get; set; }

    /// <summary>Telefone canônico de quem escreveu (o <c>from</c> do webhook, canonizado).</summary>
    public string Telefone { get; set; } = string.Empty;

    /// <summary>Usuário que o telefone representa no momento em que a mensagem chegou.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>A mensagem recebida (<c>whatsapp_mensagem</c>).</summary>
    public Guid MensagemId { get; set; }

    public string Texto { get; set; } = string.Empty;

    /// <summary>
    /// Texto da mensagem que o operador citou ao responder (normalmente um aviso de erro). Vai junto
    /// do pedido ao agente, marcado como dado — o aviso nasce de log e pode trazer texto de terceiros.
    /// </summary>
    public string? Citado { get; set; }

    public SituacaoPedidoAgente Situacao { get; set; }

    /// <summary>Sessão e turno no motor (ids do aiengine).</summary>
    public string? SessaoId { get; set; }
    public string? TurnoId { get; set; }

    /// <summary>Quantos eventos do turno já foram lidos — retomar depois de um restart sem reenviar.</summary>
    public int Cursor { get; set; }

    /// <summary>Texto do agente lido e ainda não mandado como andamento.</summary>
    public string? AndamentoPendente { get; set; }
    public DateTime? UltimoAndamentoEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? IniciadoEm { get; set; }
    public DateTime? ConcluidoEm { get; set; }
    public string? Erro { get; set; }
}

public enum SituacaoPedidoAgente
{
    Pendente = 0,
    EmAndamento = 1,
    Concluido = 2,
    Falha = 3,
    Cancelado = 4,
}
