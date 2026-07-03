namespace SMSMarica.Core.Conversas;

/// <summary>
/// Canonicalização do telefone para o WhatsApp — MESMA regra de <c>WhatsAppCliente</c> (só
/// dígitos, DDI 55 quando ausente). Garante que a conversa (inbound do webhook) e o envio
/// (outbound do cliente) usem exatamente a mesma chave de roteamento.
/// </summary>
public static class TelefoneWhatsApp
{
    public static string Canonizar(string telefone)
    {
        var d = new string([.. telefone.Where(char.IsDigit)]);
        if (d.Length <= 11 && !d.StartsWith("55")) d = "55" + d;
        return d;
    }
}
