namespace SMSMais.Data.Entities.Enums;

/// <summary>Etapa da máquina de estados DETERMINÍSTICA de verificação cadastral (desafio do
/// WhatsApp antes de entregar dados de agendamento). Valores estáveis — não renumerar.</summary>
public enum EtapaVerificacaoCadastral
{
    /// <summary>Aguardando os primeiros dígitos (≥4) do CPF.</summary>
    AguardandoCpf = 1,

    /// <summary>CPF conferiu; aguardando mês/ano de nascimento.</summary>
    AguardandoNascimento = 2,

    /// <summary>Nascimento conferiu; aguardando a confirmação do NOME (Sim/Não ou nome digitado).</summary>
    AguardandoNome = 3,
}
