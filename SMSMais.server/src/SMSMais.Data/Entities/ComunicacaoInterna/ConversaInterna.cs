using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.ComunicacaoInterna;

/// <summary>
/// Sessão de chat da Comunicação interna (ADR-0079): unidade ↔ unidade ou pessoa ↔ pessoa. É
/// ÚNICA por par não ordenado — o par é gravado com A ≤ B (ordem do Guid), então A→B e B→A caem
/// na mesma linha. Os participantes da conversa de unidade NÃO são copiados: saem na hora do
/// <c>usuario_unidade</c> das duas unidades (quem entra na equipe vê o histórico; quem sai, não).
/// </summary>
public sealed class ConversaInterna
{
    public Guid Id { get; set; }
    public TipoConversaInterna Tipo { get; set; }

    /// <summary>Unidade ↔ unidade: a menor das duas (A = B é o canal da própria equipe).</summary>
    public Guid? UnidadeAId { get; set; }
    public Guid? UnidadeBId { get; set; }

    /// <summary>Direta: o menor dos dois usuários.</summary>
    public Guid? UsuarioAId { get; set; }
    public Guid? UsuarioBId { get; set; }

    /// <summary>Denormalizado da última mensagem — ordena a lista sem varrer as mensagens.</summary>
    public DateTime? UltimaMensagemEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }

    public Unidade? UnidadeA { get; set; }
    public Unidade? UnidadeB { get; set; }
    public Usuario? UsuarioA { get; set; }
    public Usuario? UsuarioB { get; set; }
    public ICollection<MensagemInterna> Mensagens { get; set; } = [];
}
