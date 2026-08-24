using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Estado transitório da conversa de cancelamento no WhatsApp, por telefone. Vive fora
/// da Conversa (que é o chat genérico multi-operador) para não sequestrar o thread:
/// texto livre só é interpretado como "motivo" enquanto houver um estado ativo e não
/// expirado para o telefone. Removido ao concluir; expirado é ignorado.
/// </summary>
public class AgendamentoConfirmacaoEstado
{
    public Guid Id { get; set; }

    /// <summary>Telefone canônico (mesma régua de Conversa.TelefoneCanonical).</summary>
    public string TelefoneCanonical { get; set; } = string.Empty;

    public Guid ComunicacaoPacienteId { get; set; }
    public ComunicacaoPaciente? ComunicacaoPaciente { get; set; }

    public EtapaConfirmacaoAgendamento Etapa { get; set; }

    public DateTime ExpiraEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }
}
