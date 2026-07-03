using System.Globalization;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Parser do "Arquivo Agendamento (TXT)" do SISREG (<c>expo_solicitacoes</c>) — export da
/// agenda do executante. Formato: 1 linha de cabeçalho (<c>CNES;Nome;dt_ini;dt_fim;total</c>)
/// seguida de linhas de 38 campos separados por <c>;</c>. Traz tudo estruturado, inclusive o
/// código SIGTAP direto (dispensa mapear procedimento por texto). Encoding latin-1 (ISO-8859-1).
/// </summary>
public static class AgendaTxtParser
{
    public sealed record Cabecalho(string? CnesUnidade, string? NomeUnidade, DateOnly? Inicio, DateOnly? Fim, int? Total);

    public sealed record Resultado(Cabecalho Cabecalho, IReadOnlyList<MarcacaoSisreg> Marcacoes);

    // Índices das colunas (0-based) no layout de 38 campos — ver docs SISREG / mapa validado.
    private const int CodigoSolicitacao = 0;
    private const int CodigoSigtap = 2;
    private const int ProcedimentoTexto = 3;
    private const int DataAtendimento = 6;
    private const int HoraAtendimento = 7;
    private const int DataSolicitacao = 29; // data em que o pedido foi feito (seguida do operador solicitante na col. 30).
    private const int DataRegulacao = 31;   // data em que a solicitação foi regulada (seguida do operador de regulação na col. 32).
    private const int CnsPaciente = 9;
    private const int NomePaciente = 10;
    private const int TipoLogradouro = 15;
    private const int Logradouro = 16;
    private const int Complemento = 17;
    private const int Numero = 18;
    private const int Bairro = 19;
    private const int Cep = 20;
    private const int Telefone = 21;
    private const int MunicipioResidencia = 22;
    private const int CodigoIbgeResidencia = 23;
    private const int CnesUnidadeSolicitante = 26;
    private const int NomeUnidadeSolicitante = 27;
    private const int Cid = 35;
    private const int CpfMedico = 36;
    private const int NomeMedico = 37;
    private const int TotalCampos = 38;

    public static Resultado Parse(string conteudo)
    {
        var linhas = (conteudo ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var cab = new Cabecalho(null, null, null, null, null);
        var marcacoes = new List<MarcacaoSisreg>();

        foreach (var raw in linhas)
        {
            var linha = raw.TrimEnd();
            if (linha.Length == 0) continue;
            var c = linha.Split(';');

            // Cabeçalho: 5 campos, 1º é CNES (7 díg.), 5º é total numérico.
            if (c.Length == 5 && EhCnes(c[0]) && int.TryParse(c[4].Trim(), out var tot))
            {
                cab = new Cabecalho(
                    Digitos(c[0]),
                    LimparNulo(c[1]),
                    Data(c[2]),
                    Data(c[3]),
                    tot);
                continue;
            }

            // Linha de dados: 38 campos, começa com código de solicitação numérico.
            if (c.Length < TotalCampos) continue;
            var cod = c[CodigoSolicitacao].Trim();
            if (cod.Length == 0 || !cod.All(char.IsDigit)) continue;

            var dataHora = DataHora(c[DataAtendimento], c[HoraAtendimento]);
            var cpfMed = Digitos(c[CpfMedico]);

            marcacoes.Add(new MarcacaoSisreg(
                CodigoSolicitacao: cod,
                CnsPaciente: Digitos(c[CnsPaciente]) is { Length: 15 } cns ? cns : null,
                NomePaciente: LimparNulo(c[NomePaciente]),
                ProcedimentoTexto: LimparNulo(c[ProcedimentoTexto]),
                CodigoSigtap: Digitos(c[CodigoSigtap]) is { Length: > 0 } sig ? sig : null,
                CpfMedicoSolicitante: cpfMed.Length == 11 ? cpfMed : null,
                NomeMedicoSolicitante: LimparNulo(c[NomeMedico]),
                CrmMedicoSolicitante: null, // TXT não traz CRM; deriva depois (CPF→CRM).
                CnesUnidadeSolicitante: Digitos(c[CnesUnidadeSolicitante]) is { Length: 7 } cs ? cs : null,
                NomeUnidadeSolicitante: LimparNulo(c[NomeUnidadeSolicitante]),
                CnesUnidadeExecutante: cab.CnesUnidade, // o arquivo é da agenda do executante (cabeçalho).
                NomeUnidadeExecutante: cab.NomeUnidade,
                DataHoraAtendimento: dataHora,
                DataSolicitacao: Data(c[DataSolicitacao]),
                DataRegulacao: Data(c[DataRegulacao]),
                Cid: LimparNulo(c[Cid]),
                TelefonePaciente: LimparNulo(c[Telefone]),
                TipoLogradouro: LimparNulo(c[TipoLogradouro]),
                Logradouro: LimparNulo(c[Logradouro]),
                Complemento: LimparNulo(c[Complemento]),
                Numero: LimparNulo(c[Numero]),
                Bairro: LimparNulo(c[Bairro]),
                Cep: Digitos(c[Cep]) is { Length: 8 } cep ? cep : LimparNulo(c[Cep]),
                MunicipioResidencia: LimparNulo(c[MunicipioResidencia]),
                CodigoIbgeResidencia: Digitos(c[CodigoIbgeResidencia]) is { Length: >= 6 } ibge ? ibge : null,
                LinhaRaw: linha));
        }

        return new Resultado(cab, marcacoes);
    }

    private static bool EhCnes(string? s) => Digitos(s) is { Length: 7 };

    private static string Digitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    private static string? LimparNulo(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        return t.Length == 0 || t.Trim('-', ' ', '.').Length == 0 ? null : t;
    }

    /// <summary>Data em "dd.MM.yyyy" (linhas) ou "dd/MM/yyyy" (cabeçalho).</summary>
    private static DateOnly? Data(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        foreach (var fmt in new[] { "dd.MM.yyyy", "dd/MM/yyyy" })
        {
            if (DateOnly.TryParseExact(t, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        }
        return null;
    }

    private static DateTime? DataHora(string data, string hora)
    {
        var d = Data(data);
        if (d is null) return null;
        var h = (hora ?? string.Empty).Trim();
        if (!TimeOnly.TryParseExact(h, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            t = TimeOnly.MinValue;
        return DateTime.SpecifyKind(d.Value.ToDateTime(t), DateTimeKind.Unspecified);
    }
}
