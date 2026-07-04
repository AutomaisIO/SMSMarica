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

    /// <summary>
    /// True quando o número canônico é um CELULAR brasileiro: 55 + DDD (11–99) + 9 dígitos
    /// começando em 9. Fixos (10 dígitos locais) e números estrangeiros retornam false —
    /// a notificação de agendamento nem tenta enviar para eles.
    /// </summary>
    public static bool EhCelularBr(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return false;
        var d = Canonizar(telefone);
        if (d.Length != 13 || !d.StartsWith("55")) return false;
        if (d[2] == '0' || d[3] == '0') return false; // DDD 11–99
        return d[4] == '9';
    }
}
