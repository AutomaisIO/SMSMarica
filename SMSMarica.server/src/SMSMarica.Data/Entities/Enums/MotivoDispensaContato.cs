namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Por que a recepção dispensou a verificação do contato (WhatsApp) do paciente. A dispensa é o
/// registro de que a pessoa NÃO vai validar o número — e do porquê. Existe porque o gate de
/// telefone verificado travava o balcão para quem não tem celular ou não consegue confirmar o
/// código, deixando o paciente parado sem caminho de saída.
///
/// A separação que importa é: <b>existe um canal de WhatsApp utilizável e consentido?</b>
/// Motivos "sem canal" (não tem celular/WhatsApp, recusa informar, Outro) liberam a autorização
/// presencial mas mantêm resultado e laudo fora do WhatsApp — entrega presencial. Motivos "com
/// canal" (número de terceiro consentido, não conseguiu confirmar agora, sem sinal) liberam
/// também o envio, com o operador assumindo o risco. A régua vive em
/// <c>Core/Telefones/DispensaContatoRegras</c>.
/// </summary>
public enum MotivoDispensaContato
{
    /// <summary>Não possui celular. Sem canal.</summary>
    SemCelular = 1,

    /// <summary>Possui celular, mas sem WhatsApp. Sem canal.</summary>
    SemWhatsApp = 2,

    /// <summary>O número informado é de familiar/cuidador/responsável. Com canal (consentido).</summary>
    NumeroDeTerceiro = 3,

    /// <summary>Não consegue ler ou informar o código (idoso, deficiência visual, baixa
    /// alfabetização). Com canal.</summary>
    NaoConsegueConfirmar = 4,

    /// <summary>Sem sinal ou sem internet no momento do atendimento. Com canal.</summary>
    SemSinalNoMomento = 5,

    /// <summary>Paciente recusa informar ou validar o número. Sem canal.</summary>
    RecusaValidar = 6,

    /// <summary>Outro motivo — exige descrição. Sem canal (conservador: dado clínico não sai
    /// por um motivo que o sistema não conhece).</summary>
    Outro = 99,
}
