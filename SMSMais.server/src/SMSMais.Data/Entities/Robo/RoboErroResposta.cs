using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Data.Entities.Robo;

/// <summary>
/// Um atendente marcou uma RESPOSTA do robô como errada, para revisão e refino do treinamento.
/// Guarda um snapshot do texto e o assunto engajado (para agrupar por assunto), além da nota de
/// quem marcou. Não altera a mensagem já enviada — é registro para aprendizado.
/// </summary>
public class RoboErroResposta
{
    public Guid Id { get; set; }

    public Guid ConversaId { get; set; }

    /// <summary>Mensagem do robô que saiu errada (quando marcada a partir de uma bolha específica).</summary>
    public Guid? MensagemWhatsAppId { get; set; }

    /// <summary>Assunto que o robô engajou (para agrupar os erros por assunto no relatório).</summary>
    public Guid? RoboAssuntoId { get; set; }

    /// <summary>Snapshot do texto do robô no momento da marcação (preserva o que foi dito).</summary>
    public string? Trecho { get; set; }

    /// <summary>O que o atendente apontou como errado (opcional).</summary>
    public string? Nota { get; set; }

    public StatusRoboErro Status { get; set; } = StatusRoboErro.Aberto;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? RevisadoEm { get; set; }
    public Guid? RevisadoPor { get; set; }
    /// <summary>Anotação de quem revisou (o que virou treino, por que foi descartado).</summary>
    public string? RevisaoNota { get; set; }

    public Conversa? Conversa { get; set; }
    public RoboAssunto? RoboAssunto { get; set; }
    public MensagemWhatsApp? Mensagem { get; set; }
}
