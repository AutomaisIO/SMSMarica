namespace SMSMais.Core.Conversas;

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

    /// <summary>
    /// Último recurso quando o número vem sem DDD e a instância ainda não configurou o seu
    /// (<c>Instituicao.DddPadrao</c>, ADR-0043). Quem tem acesso à identidade da instituição
    /// deve passar o DDD dela para <see cref="Interpretar"/> — este valor existe só para os
    /// caminhos estáticos, e chutar 21 num município de outro estado erra o número.
    /// </summary>
    public const string DddPadraoFallback = "21";

    /// <summary>
    /// Interpreta um telefone DIGITADO POR UM HUMANO e devolve o celular canônico de 13
    /// dígitos (55 + DDD + 9 dígitos) ou o motivo da recusa. Absorve as variações que
    /// aparecem no balcão: com/sem máscara, com/sem +55, com prefixo de discagem
    /// (0 + DDD), com código de operadora (0 XX + DDD), com/sem o nono dígito e sem DDD
    /// (assume <paramref name="dddPadrao"/>). Fixo e estrangeiro são recusados — o WhatsApp só
    /// alcança celular brasileiro.
    /// </summary>
    /// <param name="dddPadrao">
    /// DDD do município desta instância. Passe <c>Instituicao.DddPadrao</c> sempre que puder;
    /// o default é só o <see cref="DddPadraoFallback"/>.
    /// </param>
    public static InterpretacaoTelefone Interpretar(string? entrada, string dddPadrao = DddPadraoFallback)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            return InterpretacaoTelefone.Recusado("Informe o telefone.");

        var d = new string([.. entrada.Where(char.IsDigit)]);

        // Código de operadora na frente do DDD: 0 + 2 dígitos (ex.: 0 21 21 99999-0000).
        if (d.Length is 14 or 15 && d[0] == '0') d = d[3..];

        // Prefixo de discagem interurbana (0 + DDD).
        d = d.TrimStart('0');

        d = d.Length switch
        {
            // Já veio com DDI (12 = celular antigo, 13 = celular atual, 12 também cobre fixo).
            12 or 13 when d.StartsWith("55") => d,
            // DDD + número (10 = fixo/celular antigo, 11 = celular atual).
            10 or 11 => "55" + d,
            // Só o número, sem DDD.
            8 or 9 => "55" + dddPadrao + d,
            _ => d,
        };

        if (!d.StartsWith("55") || d.Length is < 12 or > 13)
            return InterpretacaoTelefone.Recusado(
                "Não reconheci esse número. Informe o celular com DDD — ex.: (21) 99999-0000.");

        var fone = NormalizarNonoDigito(d);
        return EhCelularBr(fone)
            ? InterpretacaoTelefone.Aceito(fone)
            : InterpretacaoTelefone.Recusado(
                "O WhatsApp só alcança celular. Esse número parece ser fixo — informe um celular com DDD.");
    }
}

/// <summary>Resultado de <see cref="TelefoneWhatsApp.Interpretar"/>.</summary>
/// <param name="Fone">Celular canônico de 13 dígitos (55 + DDD + 9 dígitos), ou null se recusado.</param>
/// <param name="Erro">Mensagem pronta para o operador quando recusado; null quando aceito.</param>
public sealed record InterpretacaoTelefone(string? Fone, string? Erro)
{
    public bool Ok => Fone is not null;

    public static InterpretacaoTelefone Aceito(string fone) => new(fone, null);

    public static InterpretacaoTelefone Recusado(string erro) => new(null, erro);
}
