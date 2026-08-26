using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Entities.Conversas;

/// <summary>
/// Uma conversa (thread) do chat WhatsApp multi-operador — transversal a todo o SMSMais
/// (TFD, marcação, dúvidas, atendente). Agrupa as <see cref="MensagemWhatsApp"/> de um contato
/// e carrega o estado de atendimento: dono responsável (sticky), unidade, janela de 24h e
/// contador de não-lidas. No máximo uma conversa "viva" (Aberta/Pendente) por contato.
/// </summary>
public class Conversa
{
    public Guid Id { get; set; }

    public CanalConversa Canal { get; set; } = CanalConversa.WhatsApp;

    /// <summary>Telefone do cidadão em formato canônico (só dígitos, DDI 55) — chave de roteamento.</summary>
    public string TelefoneCanonical { get; set; } = string.Empty;

    /// <summary>Identidade do paciente no hub FHIR (sem FK local), quando resolvido pelo telefone.</summary>
    public Guid? PacienteId { get; set; }

    /// <summary>Nome do contato (snapshot do <c>profile.name</c> do WhatsApp), quando sem paciente.</summary>
    public string? NomeContato { get; set; }

    /// <summary>Assunto/categoria opcional para organizar as filas (não gateia regras).</summary>
    public AssuntoConversa? Assunto { get; set; }

    public StatusConversa Status { get; set; } = StatusConversa.Aberta;

    /// <summary>Operador "dono" atual (sticky). <c>null</c> = na triagem, sem responsável.</summary>
    public Guid? OperadorResponsavelId { get; set; }

    /// <summary>Unidade responsável pela fila. <c>null</c> = triagem geral.</summary>
    public Guid? UnidadeId { get; set; }

    /// <summary>Fim da janela de 24h da Meta (última mensagem do cidadão + 24h). Dentro dela,
    /// texto livre; fora, só template HSM aprovado. <c>null</c> = nunca respondida (só template).</summary>
    public DateTime? JanelaExpiraEm { get; set; }

    // Denormalizados para a lista de conversas (evita agregação por thread).
    public DateTime? UltimaMensagemEm { get; set; }
    public DirecaoMensagem? UltimaMensagemDirecao { get; set; }
    public string? UltimaMensagemPreview { get; set; }

    /// <summary>Mensagens do cidadão ainda não lidas pelo operador. Zerado ao abrir a thread.</summary>
    public int NaoLidas { get; set; }

    // ---- Robô de atendimento ----
    /// <summary>Início da JANELA de 24h corrente (âncora). Escrito só pelo webhook quando uma
    /// janela nova abre (primeiro contato ou depois da anterior expirar). Base da trava
    /// "humano-por-janela": o robô se cala se houve resposta/atribuição humana desde aqui.</summary>
    public DateTime? JanelaAbertaEm { get; set; }

    /// <summary>Quantas vezes o robô já respondeu nesta janela (zerado ao abrir janela nova) —
    /// limite antes do hand-off por "X interações sem resolver".</summary>
    public int RoboInteracoesNaJanela { get; set; }

    /// <summary>Assunto que o robô engajou nesta conversa (para o operador ver). <c>null</c> =
    /// robô não atuou.</summary>
    public Guid? RoboAssuntoId { get; set; }

    public DateTime PrimeiroContatoEm { get; set; }

    /// <summary>Token de concorrência (xmin do Postgres) — protege takeover simultâneo.</summary>
    public uint RowVersion { get; set; }

    // Auditoria (criação / edição / exclusão lógica)
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    // Navegações
    public Usuario? OperadorResponsavel { get; set; }
    public Unidade? Unidade { get; set; }
    public ICollection<MensagemWhatsApp> Mensagens { get; set; } = [];
    public ICollection<ConversaEvento> Eventos { get; set; } = [];
}
