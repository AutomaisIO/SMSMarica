namespace SMSMarica.Data.Entities;

/// <summary>
/// Registro global de um número de telefone que já foi validado (via OTP WhatsApp)
/// em algum momento — independente do cadastro. A validação é do NÚMERO em si: uma vez
/// validado, qualquer cadastro (paciente, médico, usuário, motorista) com esse número
/// exibe o selo de "validado". Pode vir do painel (operadora dispara o OTP) ou do app
/// do cidadão (PWA), onde o login por OTP também marca o número.
/// </summary>
public class NumeroValidado
{
    public Guid Id { get; set; }

    /// <summary>Número canônico: apenas dígitos com DDI Brasil (ex.: 5521999990000).</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime ValidadoEm { get; set; }

    /// <summary>Origem da validação: <c>painel</c> ou <c>pwa-cidadao</c>.</summary>
    public string Origem { get; set; } = "painel";

    /// <summary>Usuário do painel que disparou/confirmou (null quando veio do PWA cidadão).</summary>
    public Guid? ValidadoPor { get; set; }
}
