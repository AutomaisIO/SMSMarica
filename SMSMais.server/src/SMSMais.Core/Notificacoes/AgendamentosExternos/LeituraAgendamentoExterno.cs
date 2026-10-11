using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SMSMais.Core.Common.Tempo;

namespace SMSMais.Core.Notificacoes.AgendamentosExternos;

/// <summary>
/// Como ler, dos espelhos do SER/SERNIT/ESUS SG, o QUANDO e o ONDE de um agendamento — cada sistema
/// escreve de um jeito (medido em produção em 10/10/2026):
/// <list type="bullet">
/// <item>SER: <c>agendado_para_texto</c> = "19/10/2026" (só o dia — o export descarta a hora); a hora
/// está no evento "Agendar" do histórico: "Data do agendamento: 19/10/2026 08:30 |". Nos 128
/// agendamentos futuros medidos, os 128 tinham o evento com a mesma data.</item>
/// <item>SERNIT: <c>agendado_para_texto</c> = "30/10/2026 09:15 - APADA - ASSOCIAÇÃO DE PAIS…" (dia,
/// hora e local juntos); <c>unidade_executora</c> vem vazia.</item>
/// <item>ESUS SG: <c>data_hora_agendada_texto</c> = "13/10/2026 13:25:00" e <c>unidade_executora</c>
/// = "OFTALMOCLÍNICA SÃO GONÇALO - 2291525" (o CNES colado no nome).</item>
/// </list>
/// Texto que não se lê vira nulo: dado ausente é melhor que dado inventado num aviso ao paciente.
/// </summary>
public static partial class LeituraAgendamentoExterno
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Hora assumida quando o sistema não informa — só para as guardas de "já passou";
    /// nunca é dita ao paciente.</summary>
    internal static readonly TimeOnly HoraPresumida = new(6, 0);

    /// <summary>"dd/MM/yyyy[ HH:mm[:ss]][ - LOCAL]" → dia, hora e o que vem depois do primeiro " - ".</summary>
    public static (DateOnly? Data, TimeOnly? Hora, string? Resto) DataHora(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return (null, null, null);
        var m = RegexDataHora().Match(texto);
        if (!m.Success
            || !DateOnly.TryParseExact(m.Groups["data"].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var data))
            return (null, null, null);

        TimeOnly? hora = null;
        if (m.Groups["hh"].Success
            && int.TryParse(m.Groups["hh"].Value, out var hh) && int.TryParse(m.Groups["mm"].Value, out var mm)
            && hh is >= 0 and < 24 && mm is >= 0 and < 60)
            hora = new TimeOnly(hh, mm);

        string? resto = null;
        var idx = texto.IndexOf(" - ", m.Index + m.Length, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var r = Espacos(texto[(idx + 3)..]);
            if (r.Length > 0) resto = r;
        }
        return (data, hora, resto);
    }

    /// <summary>A hora do evento "Agendar" do SER — só quando o dia do evento é o mesmo do
    /// agendamento (evento de uma marcação anterior não vale para a atual).</summary>
    public static TimeOnly? HoraDoEventoAgendar(string? observacao, DateOnly data)
    {
        if (string.IsNullOrWhiteSpace(observacao)) return null;
        var m = RegexEventoAgendar().Match(observacao);
        if (!m.Success) return null;
        var (dia, hora, _) = DataHora($"{m.Groups["data"].Value} {m.Groups["hora"].Value}");
        return dia == data ? hora : null;
    }

    /// <summary>Tira o CNES que o ESUS cola no nome da unidade ("ABRAE  2297523", "OFTALMOCLÍNICA SÃO
    /// GONÇALO - 2291525") — o paciente lê o nome, não o código.</summary>
    public static string? LocalEsusSg(string? unidadeExecutora)
    {
        if (string.IsNullOrWhiteSpace(unidadeExecutora)) return null;
        var limpo = Espacos(RegexCnesNoFim().Replace(unidadeExecutora, string.Empty));
        return limpo.Length == 0 ? null : limpo;
    }

    /// <summary>O instante (UTC) do atendimento, para as guardas de "data futura". Sem hora, usa
    /// <see cref="HoraPresumida"/>.</summary>
    public static DateTime InstanteUtc(DateOnly data, TimeOnly? hora) =>
        FusoBrasilia.DeBrasiliaParaUtc(data.ToDateTime(hora ?? HoraPresumida));

    /// <summary>"19/10/2026 às 08:30h" — ou só o dia, quando a hora não veio.</summary>
    public static string DataParaPaciente(DateOnly data, TimeOnly? hora) =>
        hora is { } h
            ? $"{data.ToString("dd/MM/yyyy", PtBr)} às {h.ToString("HH:mm", PtBr)}h"
            : data.ToString("dd/MM/yyyy", PtBr);

    /// <summary>A chave de casamento de nomes de unidade: sem acento, maiúsculo, espaços colapsados.</summary>
    public static string NormalizarNome(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return string.Empty;
        var decomposto = nome.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return Espacos(sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant());
    }

    internal static string Espacos(string? t) =>
        string.IsNullOrWhiteSpace(t) ? string.Empty : RegexEspacos().Replace(t.Trim(), " ");

    [GeneratedRegex(@"(?<data>\d{2}/\d{2}/\d{4})(?:\s+(?<hh>\d{2}):(?<mm>\d{2})(?::\d{2})?)?")]
    private static partial Regex RegexDataHora();

    [GeneratedRegex(@"Data do agendamento:\s*(?<data>\d{2}/\d{2}/\d{4})\s+(?<hora>\d{2}:\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex RegexEventoAgendar();

    [GeneratedRegex(@"(?:\s+-\s*|\s+)\d{7}\s*$")]
    private static partial Regex RegexCnesNoFim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacos();
}
