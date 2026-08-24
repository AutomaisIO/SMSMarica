using System.Globalization;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Parser do export de agendamentos do SISREG (<c>expo_solicitacoes</c>) — a agenda do
/// executante. Aceita os DOIS formatos que o SISREG entrega, pois as linhas de dados são
/// idênticas (38 campos separados por <c>;</c>, datas em <c>dd.MM.yyyy</c>):
/// <list type="bullet">
///   <item><b>TXT</b>: 1ª linha é o cabeçalho da unidade (<c>CNES;Nome;dt_ini;dt_fim;total</c>);
///   traz o CNES do executante. Encoding latin-1 (ISO-8859-1).</item>
///   <item><b>CSV</b>: 1ª linha é o cabeçalho de COLUNAS (<c>solicitacao;codigo_interno;…</c>),
///   sem a linha da unidade — o executante vem do <b>nome do arquivo</b> (a extração é por
///   unidade). Sai em ASCII (sem acentos).</item>
/// </list>
/// Ambos trazem o código SIGTAP direto (dispensa mapear procedimento por texto).
/// </summary>
public static class AgendaTxtParser
{
    public sealed record Cabecalho(string? CnesUnidade, string? NomeUnidade, DateOnly? Inicio, DateOnly? Fim, int? Total);

    /// <summary>Linha que o parser não conseguiu ler como marcação — não some em silêncio, vira
    /// falha durável para o operador ver e corrigir. <see cref="Numero"/> é 1-based no arquivo.</summary>
    public sealed record LinhaRejeitada(int Numero, string LinhaRaw, string Motivo);

    public sealed record Resultado(
        Cabecalho Cabecalho,
        IReadOnlyList<MarcacaoSisreg> Marcacoes,
        IReadOnlyList<LinhaRejeitada> Rejeitadas);

    // Índices das colunas (0-based) no layout de 38 campos — ver docs SISREG / mapa validado.
    private const int CodigoSolicitacao = 0;
    /// <summary>O <c>pa</c> do SISREG. Vem VAZIO em ~1/3 das linhas — por isso é opcional.</summary>
    private const int CodigoProcedimentoSisreg = 1;
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

    /// <summary>Veredito de "isto é mesmo um export de agendamentos do SISREG?".</summary>
    /// <param name="Reconhecido">False = arquivo incompatível; descartar inteiro, sem parsear linha a linha.</param>
    /// <param name="Motivo">Por que não foi reconhecido (vazio quando reconhecido).</param>
    public sealed record Assinatura(bool Reconhecido, string Motivo);

    /// <summary>
    /// Decide se o conteúdo é um export de agendamentos do SISREG, olhando a PRIMEIRA linha de
    /// dados (cabeçalhos de unidade/colunas e linhas vazias são pulados, como no <see cref="Parse"/>).
    /// <para>
    /// Contagem de coluna sozinha é um teste fraco — qualquer CSV alheio com 38 campos passaria e
    /// geraria centenas de falhas de lixo. Então a checagem é de FORMA: onde o layout exige dígito
    /// tem que ser dígito, e onde exige data tem que ser data válida em <c>dd.MM.yyyy</c>.
    /// </para>
    /// <para>
    /// Só campos ESTRUTURAIS entram no teste. Campos que legitimamente vêm vazios em arquivo bom
    /// (CNS, telefone, endereço, médico) NÃO podem reprovar o arquivo — isso é falha de LINHA, que
    /// o operador resolve na aba Erros, não motivo para jogar o arquivo fora.
    /// </para>
    /// </summary>
    public static Assinatura Reconhecer(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            return new(false, "Arquivo vazio.");

        var linhas = (conteudo ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var raw in linhas)
        {
            var linha = raw.TrimEnd();
            if (linha.Length == 0) continue;
            var c = linha.Split(';');
            if (EhCabecalhoColunas(c)) continue;
            if (c.Length == 5 && EhCnes(c[0]) && int.TryParse(c[4].Trim(), out _)) continue;

            // Primeira linha candidata a dados: ela decide o arquivo inteiro.
            if (c.Length < TotalCampos)
                return new(false, $"A primeira linha de dados tem {c.Length} campo(s); o export de agendamentos do SISREG tem {TotalCampos}.");

            if (!SoDigitosNaoVazio(c[CodigoSolicitacao]))
                return new(false, $"O nº da solicitação (1ª coluna) deveria ser numérico, veio \"{Amostra(c[CodigoSolicitacao])}\".");

            if (!SoDigitosNaoVazio(c[CodigoSigtap]))
                return new(false, $"O código SIGTAP (3ª coluna) deveria ser numérico, veio \"{Amostra(c[CodigoSigtap])}\".");

            // O sinal mais forte: data de atendimento no formato do SISREG.
            if (Data(c[DataAtendimento]) is null)
                return new(false, $"A data de atendimento (7ª coluna) deveria estar em dd.MM.aaaa, veio \"{Amostra(c[DataAtendimento])}\".");

            if (Digitos(c[CnesUnidadeSolicitante]) is not { Length: 7 })
                return new(false, $"O CNES do solicitante (27ª coluna) deveria ter 7 dígitos, veio \"{Amostra(c[CnesUnidadeSolicitante])}\".");

            return new(true, string.Empty);
        }

        return new(false, "Não há nenhuma linha de dados — só cabeçalho ou linhas em branco.");
    }

    private static bool SoDigitosNaoVazio(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        return t.Length > 0 && t.All(char.IsDigit);
    }

    /// <summary>Trecho curto do valor para a mensagem de erro (não despeja a linha toda).</summary>
    private static string Amostra(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        return t.Length <= 20 ? t : t[..20] + "…";
    }

    /// <param name="conteudo">Texto integral do arquivo (TXT ou CSV).</param>
    /// <param name="nomeArquivo">Nome do arquivo enviado — usado só no CSV, para derivar a
    /// unidade executante (que não vem nas linhas). Ex.: "CDT DR ALBERTO…-20260703.csv".</param>
    public static Resultado Parse(string conteudo, string? nomeArquivo = null)
    {
        var linhas = (conteudo ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        // Sem cabeçalho de unidade (caso CSV), o executante vem do nome do arquivo. Um cabeçalho
        // real (TXT) sobrescreve isto adiante com o CNES + nome de verdade.
        var cab = new Cabecalho(null, NomeExecutanteDoArquivo(nomeArquivo), null, null, null);
        var marcacoes = new List<MarcacaoSisreg>();
        var rejeitadas = new List<LinhaRejeitada>();

        for (var idx = 0; idx < linhas.Length; idx++)
        {
            var linha = linhas[idx].TrimEnd();
            if (linha.Length == 0) continue;
            var c = linha.Split(';');

            // Cabeçalho de COLUNAS do CSV (1ª célula "solicitacao"): ignora explicitamente.
            if (EhCabecalhoColunas(c)) continue;

            // Cabeçalho da unidade (TXT): 5 campos, 1º é CNES (7 díg.), 5º é total numérico.
            if (c.Length == 5 && EhCnes(c[0]) && int.TryParse(c[4].Trim(), out var tot))
            {
                cab = new Cabecalho(
                    Digitos(c[0]),
                    LimparNulo(c[1]) ?? cab.NomeUnidade,
                    Data(c[2]),
                    Data(c[3]),
                    tot);
                continue;
            }

            // Linha de dados: 38 campos, começa com código de solicitação numérico. O que não
            // encaixa é REJEITADO com motivo (antes sumia calado e o registro nunca era importado).
            if (c.Length < TotalCampos)
            {
                rejeitadas.Add(new LinhaRejeitada(idx + 1, linha,
                    $"Linha com {c.Length} campos — o layout do SISREG tem {TotalCampos}. Arquivo truncado ou fora do formato de agendamentos."));
                continue;
            }
            var cod = c[CodigoSolicitacao].Trim();
            if (cod.Length == 0 || !cod.All(char.IsDigit))
            {
                rejeitadas.Add(new LinhaRejeitada(idx + 1, linha,
                    cod.Length == 0
                        ? "Linha sem o nº da solicitação (1ª coluna vazia)."
                        : $"Nº da solicitação inválido (\"{cod}\") — esperado só dígitos."));
                continue;
            }

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
                LinhaRaw: linha,
                CodigoProcedimentoSisreg: LimparNulo(c[CodigoProcedimentoSisreg])));
        }

        return new Resultado(cab, marcacoes, rejeitadas);
    }

    private static bool EhCnes(string? s) => Digitos(s) is { Length: 7 };

    /// <summary>Linha de cabeçalho de colunas do CSV (começa por "solicitacao").</summary>
    private static bool EhCabecalhoColunas(string[] c) =>
        c.Length >= 2 && string.Equals(c[0].Trim(), "solicitacao", StringComparison.OrdinalIgnoreCase);

    /// <summary>Nome da unidade executante a partir do nome do arquivo, tirando extensão e o
    /// sufixo de data "-yyyymmdd" (ex.: "USF JOSEFA XAVIER LEAL-20260703.csv" → "USF JOSEFA
    /// XAVIER LEAL"). Retorna em MAIÚSCULAS (como as unidades são criadas na importação).</summary>
    private static string? NomeExecutanteDoArquivo(string? nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo)) return null;
        var nome = Path.GetFileNameWithoutExtension(nomeArquivo.Trim());
        // Corta um bloco final de exatamente 8 dígitos precedido de separador (-, _ ou espaço).
        var i = nome.Length;
        while (i > 0 && char.IsDigit(nome[i - 1])) i--;
        if (nome.Length - i == 8 && i > 0 && nome[i - 1] is '-' or '_' or ' ')
            nome = nome[..(i - 1)];
        nome = nome.Trim().Trim('-', '_').Trim();
        return nome.Length == 0 ? null : nome.ToUpperInvariant();
    }

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
