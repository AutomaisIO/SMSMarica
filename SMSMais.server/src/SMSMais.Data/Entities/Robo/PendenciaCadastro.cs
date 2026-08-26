using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Robo;

/// <summary>
/// Pendência de ajuste de cadastro levantada no atendimento — hoje quando o cidadão diz que o
/// número NÃO é dele ("não sou essa pessoa"). O robô não corrige nada: apenas registra (com o
/// vínculo declarado — parente/responsável/sem vínculo) para a recepção resolver depois, na
/// tela de Pendências de Cadastro. É a "lista de números errados".
/// </summary>
public class PendenciaCadastro
{
    public Guid Id { get; set; }

    /// <summary>Conversa de origem, quando veio do chat/robô.</summary>
    public Guid? ConversaId { get; set; }

    /// <summary>Telefone (canônico) que recebeu a mensagem errada.</summary>
    public string TelefoneCanonical { get; set; } = string.Empty;

    /// <summary>Paciente do cadastro a que a mensagem se referia (o "dono errado").</summary>
    public Guid? PacienteId { get; set; }

    public TipoPendenciaCadastro Tipo { get; set; } = TipoPendenciaCadastro.NumeroErrado;

    public VinculoContato Vinculo { get; set; } = VinculoContato.NaoInformado;

    public string? Observacao { get; set; }

    public StatusPendenciaCadastro Status { get; set; } = StatusPendenciaCadastro.Aberta;

    public DateTime CriadoEm { get; set; }

    /// <summary><c>null</c> = criada pelo robô (automação); preenchido = registro manual do operador.</summary>
    public Guid? CriadoPor { get; set; }

    public DateTime? ResolvidoEm { get; set; }
    public Guid? ResolvidoPor { get; set; }
    public string? ResolucaoNota { get; set; }

    public Conversa? Conversa { get; set; }
}
