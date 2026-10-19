using System.Text.RegularExpressions;

namespace SMSMais.Core.Notificacoes.WhatsApp.Conta;

/// <summary>Erro da Meta que diz respeito à CONTA inteira, não ao número do paciente.</summary>
/// <param name="Codigo">Código da Meta (ex.: "131042").</param>
/// <param name="Explicacao">O que aconteceu e onde resolver, em português, para a faixa do painel.</param>
public sealed record ErroDeContaWhatsApp(string Codigo, string Explicacao);

/// <summary>
/// Separa o erro que é da CONTA do WhatsApp Business (pagamento, bloqueio, token) do erro que é de
/// UM número (não tem WhatsApp, não aceitou os termos). O primeiro para tudo de uma vez; o segundo,
/// só aquela mensagem.
///
/// <para><b>Por que existe:</b> em 19/10/2026 a conta ficou sem pagamento das 10:29 às ~12:50. A
/// Meta recusou 694 mensagens com 131042 e ninguém soube: a fila tratou cada recusa como falha
/// daquela mensagem (gastando as tentativas) e o aviso de erro — que também sai pelo WhatsApp —
/// morreu no mesmo 131042, enquanto o registro dizia "enviado".</para>
///
/// <para>Casa os dois envelopes de erro em uso: o do Automais.Zap ("{message} (code N)") e o antigo,
/// direto da Meta ("(N) message") — o mesmo cuidado de <c>ComunicacaoPacienteService.ErroPermanente</c>.</para>
/// </summary>
public static partial class ErroContaWhatsApp
{
    private static readonly Dictionary<string, string> Codigos = new(StringComparer.Ordinal)
    {
        ["131042"] = "A Meta recusou os envios por problema de PAGAMENTO da conta do WhatsApp Business. "
                     + "Regularize em Meta Business → Faturamento (forma de pagamento/limite de crédito).",
        ["131031"] = "A Meta bloqueou ou travou a conta do WhatsApp Business. Veja o aviso no Gerenciador do WhatsApp.",
        ["368"] = "A Meta bloqueou temporariamente a conta por violação de política. Veja o aviso no Gerenciador do WhatsApp.",
        ["131048"] = "A Meta limitou os envios deste número por qualidade (muitas mensagens bloqueadas ou "
                     + "denunciadas). Os envios voltam quando o limite cair.",
        ["190"] = "O token de acesso à Meta venceu ou foi revogado. Gere um novo no Automais.Zap (Meta → Credenciais).",
        ["133010"] = "O número do WhatsApp não está registrado na Meta. Registre de novo no Automais.Zap.",
    };

    [GeneratedRegex(@"\(#?(\d{3,6})\)|code (\d{3,6})")]
    private static partial Regex RegexCodigo();

    /// <summary>O erro de conta contido no texto, ou null quando o erro é de outro tipo (ou não há erro).</summary>
    public static ErroDeContaWhatsApp? Classificar(string? erro)
    {
        if (string.IsNullOrWhiteSpace(erro)) return null;
        foreach (Match m in RegexCodigo().Matches(erro))
        {
            var codigo = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            if (Codigos.TryGetValue(codigo, out var explicacao))
                return new ErroDeContaWhatsApp(codigo, explicacao);
        }
        return null;
    }
}
