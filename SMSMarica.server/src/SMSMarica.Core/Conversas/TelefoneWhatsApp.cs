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
    /// Completa o NONO DÍGITO de celular BR no formato antigo: 55 + DDD + 8 dígitos locais
    /// começando em 6–9 (celulares pré-2012; fixos começam em 2–5) ganham o 9 após o DDD.
    /// NÃO altera a chave de roteamento das Conversas (<see cref="Canonizar"/>) — uso restrito
    /// ao ENVIO, onde a Meta espera o formato atual de 13 dígitos.
    /// </summary>
    public static string NormalizarNonoDigito(string telefone)
    {
        var d = Canonizar(telefone);
        if (d.Length == 12 && d.StartsWith("55")
            && d[2] != '0' && d[3] != '0' // DDD 11–99
            && d[4] >= '6')
            d = d[..4] + "9" + d[4..];
        return d;
    }

    /// <summary>
    /// True quando o número é um CELULAR brasileiro: 55 + DDD (11–99) + 9 dígitos começando
    /// em 9 — aceitando também o formato ANTIGO sem o nono dígito (normalizado antes de
    /// validar). Fixos e números estrangeiros retornam false — a notificação de agendamento
    /// nem tenta enviar para eles.
    /// </summary>
    public static bool EhCelularBr(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone)) return false;
        var d = NormalizarNonoDigito(telefone);
        if (d.Length != 13 || !d.StartsWith("55")) return false;
        if (d[2] == '0' || d[3] == '0') return false; // DDD 11–99
        return d[4] == '9';
    }
}
