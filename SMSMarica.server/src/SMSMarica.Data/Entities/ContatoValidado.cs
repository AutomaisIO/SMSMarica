namespace SMSMarica.Data.Entities;

/// <summary>
/// Contato principal (WhatsApp) validado de uma PESSOA, ancorado pelo <see cref="Cpf"/>.
/// O CPF é a chave universal da pessoa no sistema (liga Patient/Practitioner do hub FHIR
/// e Usuario/Motorista do smsmarica, que não têm FK entre si). Cada CPF tem no máximo um
/// contato validado, e cada número pertence a no máximo uma pessoa (dois UNIQUE): é o
/// número pelo qual a SMS fala com a pessoa via WhatsApp. Pode ser validado pelo painel
/// (operadora dispara o OTP) ou pelo app do cidadão (login por OTP marca o contato).
/// </summary>
public class ContatoValidado
{
    public Guid Id { get; set; }

    /// <summary>CPF da pessoa (11 dígitos). UNIQUE — 1 contato principal por pessoa.</summary>
    public string Cpf { get; set; } = string.Empty;

    /// <summary>Número canônico: dígitos com DDI Brasil (ex.: 5521999990000). UNIQUE entre pessoas.</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime ValidadoEm { get; set; }

    /// <summary>Origem da validação: <c>painel</c> ou <c>pwa-cidadao</c>.</summary>
    public string Origem { get; set; } = "painel";

    /// <summary>Usuário do painel que disparou/confirmou (null quando veio do PWA cidadão).</summary>
    public Guid? ValidadoPor { get; set; }
}
