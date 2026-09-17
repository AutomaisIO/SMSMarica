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

    /// <summary>Nome confirmado; aguardando o VÍNCULO — o número é do próprio paciente ou de quem
    /// recebe por ele (mãe, pai, responsável, outro parente). Fica gravado no cadastro (LGPD).</summary>
    AguardandoVinculo = 6,

    /// <summary>Os dados não conferiram; explicamos que são do PACIENTE e perguntamos se a pessoa
    /// quer tentar de novo. "Sim" (ou já mandar os dígitos) recomeça o ciclo do zero.</summary>
    AguardandoNovaTentativa = 4,

    /// <summary>Chances esgotadas (ou ambiguidade insolúvel): a máquina se cala e o estado FICA,
    /// justamente para que reenviar dígitos não recrie o diálogo com o contador zerado. Resolve-se
    /// no posto (ou por um atendente).</summary>
    Esgotado = 5,
}
