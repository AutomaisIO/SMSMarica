using System.Globalization;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Escalas;

/// <summary>
/// Lê o CSV de escalas ambulatoriais do SISREG (tela <c>cons_escalas</c>, etapa
/// <c>EXPORTAR_ESCALAS</c>) — a grade de OFERTA da rede.
///
/// <para>Formato medido em 04/09/2026 sobre o arquivo real: <b>34 colunas</b> separadas por
/// <c>;</c>, UTF-8 sem BOM, com uma linha de cabeçalho de colunas. Sem cabeçalho de unidade (ao
/// contrário do TXT de agendamentos): cada linha carrega o próprio CNES.</para>
///
/// <para>Segue o molde do <c>AgendaTxtParser</c> de propósito: índices de coluna como constantes
/// nomeadas, <see cref="Reconhecer"/> validando <b>forma</b> (não só contagem de colunas) e linha
/// ruim virando <see cref="LinhaRejeitada"/> durável em vez de sumir calada.</para>
///
/// <para><b>Conferido contra o arquivo real</b> (17.469 linhas, 04/09/2026): 17.452 escalas lidas e
/// <b>17 rejeitadas</b> — todas por vigência <c>---</c>, defeito de cadastro do próprio SISREG.
/// Nenhuma delas é <c>ATIVA</c>: o total de ativas bate exatamente (1.639), assim como as 934
/// vigentes do dia, 34 CNES, 199 CPFs, 119 procedimentos, 882 grupos e as vagas 814/1.629/3.171.
/// Ou seja, a rejeição não tira nada da oferta — ela expõe lixo histórico.</para>
/// </summary>
public static class EscalasCsvParser
{
    /// <summary>Uma linha de escala já tipada, antes de virar entidade.</summary>
    public sealed record LinhaEscala(
        string CodigoEscala,
        string Cnes,
        string UnidadeNome,
        string ProfissionalCpf,
        string ProfissionalNome,
        string? CboCodigo,
        string? CboDescricao,
        string ProcedimentoCodigo,
        string ProcedimentoNome,
        string? ProcedimentoSigtap,
        DayOfWeek DiaSemana,
        TimeOnly HoraInicio,
        TimeOnly HoraFim,
        int VagasPrimeiraVez,
        int MinutosPrimeiraVez,
        int VagasRetorno,
        int MinutosRetorno,
        int VagasReserva,
        int MinutosReserva,
        DateOnly VigenciaInicio,
        DateOnly VigenciaFim,
        StatusEscalaSisreg Status,
        bool AgendaLocal,
        bool QuebraAutomatica,
        string? OperadorCriador,
        string? OperadorModificador,
        DateOnly? InseridaEm,
        DateOnly? AlteradaEm,
        DateOnly? AtivadaEm)
    {
        /// <summary>Código terminado em <c>000</c> é GRUPO e expande em itens no agendamento.</summary>
        public bool EhGrupo => ProcedimentoCodigo.EndsWith("000", StringComparison.Ordinal);

        public int VagasTotal => VagasPrimeiraVez + VagasRetorno + VagasReserva;
    }

    public sealed record LinhaRejeitada(int Numero, string LinhaRaw, string Motivo);

    public sealed record Resultado(
        IReadOnlyList<LinhaEscala> Escalas,
        IReadOnlyList<LinhaRejeitada> Rejeitadas);

    public sealed record Assinatura(bool Reconhecido, string Motivo);

    // Índices (0-based) do layout de 34 colunas.
    private const int CodEscala = 0;
    private const int CodCentral = 1;
    private const int DescCentral = 2;
    private const int CpfProfissional = 3;
    private const int NomeProfissional = 4;
    private const int CodCbo = 5;
    private const int DescCbo = 6;
    private const int CodCnes = 7;
    private const int DescCnes = 8;
    private const int CodProcedimentoInterno = 9;
    private const int DescProcedimentoInterno = 10;
    private const int CodProcedimentoUnificado = 11;
    private const int SiglaDiaSemana = 12;
    private const int VagasPrimeiraVez = 13;
    private const int MinutosPrimeiraVez = 14;
    private const int VagasRetorno = 15;
    private const int MinutosRetorno = 16;
    private const int VagasReserva = 17;
    private const int MinutosReserva = 18;
    private const int QuebraAutomatica = 19;
    private const int AgendaLocal = 20;
    private const int VigenciaInicial = 21;
    private const int VigenciaFinal = 22;
    private const int HoraInicial = 23;
    private const int HoraFinal = 24;
    private const int OperadorCriador = 25;
    private const int OperadorModificador = 26;
    private const int DataUltimaAlteracao = 27;
    private const int DataInsercao = 30;
    private const int Status = 29;
    private const int DataUltimaAtivacao = 32;
    private const int TotalCampos = 34;

    /// <summary>
    /// Siglas de dia da semana do SISREG. <b>Confiáveis:</b> nas escalas de um dia só (vigência
    /// inicial = final), a sigla bateu com o weekday da data em 100% das 17.469 linhas medidas.
    /// </summary>
    private static readonly Dictionary<string, DayOfWeek> Dias = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DOM"] = DayOfWeek.Sunday,
        ["SEG"] = DayOfWeek.Monday,
        ["TER"] = DayOfWeek.Tuesday,
        ["QUA"] = DayOfWeek.Wednesday,
        ["QUI"] = DayOfWeek.Thursday,
        ["SEX"] = DayOfWeek.Friday,
        ["SAB"] = DayOfWeek.Saturday,
    };

    private static readonly Dictionary<string, StatusEscalaSisreg> Status_ = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ATIVA"] = StatusEscalaSisreg.Ativa,
        ["INATIVA"] = StatusEscalaSisreg.Inativa,
        ["EXPIRADA"] = StatusEscalaSisreg.Expirada,
        ["EXCLUIDA"] = StatusEscalaSisreg.Excluida,
        ["EXCLUÍDA"] = StatusEscalaSisreg.Excluida,
    };

    /// <summary>
    /// "Isto é mesmo o export de escalas?" — olha a primeira linha de dados e checa FORMA.
    ///
    /// <para>Contagem de colunas sozinha é teste fraco: qualquer CSV alheio com 34 campos passaria e
    /// encheria a tela de falhas de lixo. Então exige código de escala numérico, CNES de 7 dígitos e
    /// sigla de dia da semana reconhecida — os três campos estruturais que um arquivo bom nunca
    /// deixa vazios.</para>
    /// </summary>
    public static Assinatura Reconhecer(string conteudo)
    {
        if (string.IsNullOrWhiteSpace(conteudo)) return new(false, "Arquivo vazio.");

        foreach (var bruta in Linhas(conteudo))
        {
            var c = bruta.Split(';');
            if (EhCabecalhoColunas(c)) continue;

            if (c.Length < TotalCampos)
            {
                return new(false,
                    $"Primeira linha de dados tem {c.Length} campos — o export de escalas do SISREG "
                    + $"tem {TotalCampos}. Provavelmente é outro arquivo.");
            }

            if (!SoDigitos(c[CodEscala]))
                return new(false, $"Código de escala inválido (\"{Curto(c[CodEscala])}\") — esperado só dígitos.");
            if (Digitos(c[CodCnes]).Length != 7)
                return new(false, $"CNES inválido (\"{Curto(c[CodCnes])}\") — esperado 7 dígitos.");
            if (!Dias.ContainsKey(c[SiglaDiaSemana].Trim()))
                return new(false, $"Dia da semana desconhecido (\"{Curto(c[SiglaDiaSemana])}\").");

            return new(true, string.Empty);
        }

        return new(false, "Arquivo sem nenhuma linha de dados.");
    }

    public static Resultado Parse(string conteudo)
    {
        var escalas = new List<LinhaEscala>();
        var rejeitadas = new List<LinhaRejeitada>();
        var numero = 0;

        foreach (var bruta in Linhas(conteudo ?? string.Empty))
        {
            numero++;
            var c = bruta.Split(';');
            if (EhCabecalhoColunas(c)) continue;

            if (c.Length < TotalCampos)
            {
                rejeitadas.Add(new(numero, bruta,
                    $"Linha com {c.Length} campos — o layout de escalas tem {TotalCampos}."));
                continue;
            }

            var codigo = c[CodEscala].Trim();
            if (codigo.Length == 0 || !SoDigitos(codigo))
            {
                rejeitadas.Add(new(numero, bruta,
                    codigo.Length == 0
                        ? "Linha sem o código da escala (1ª coluna vazia)."
                        : $"Código de escala inválido (\"{Curto(codigo)}\") — esperado só dígitos."));
                continue;
            }

            if (!Dias.TryGetValue(c[SiglaDiaSemana].Trim(), out var dia))
            {
                rejeitadas.Add(new(numero, bruta,
                    $"Dia da semana desconhecido (\"{Curto(c[SiglaDiaSemana])}\")."));
                continue;
            }

            if (!Status_.TryGetValue(c[Status].Trim(), out var status))
            {
                rejeitadas.Add(new(numero, bruta, $"Status desconhecido (\"{Curto(c[Status])}\")."));
                continue;
            }

            if (Hora(c[HoraInicial]) is not { } horaIni || Hora(c[HoraFinal]) is not { } horaFim)
            {
                rejeitadas.Add(new(numero, bruta,
                    $"Horário inválido (\"{Curto(c[HoraInicial])}\" - \"{Curto(c[HoraFinal])}\")."));
                continue;
            }

            if (Data(c[VigenciaInicial]) is not { } vigIni || Data(c[VigenciaFinal]) is not { } vigFim)
            {
                rejeitadas.Add(new(numero, bruta,
                    $"Vigência inválida (\"{Curto(c[VigenciaInicial])}\" - \"{Curto(c[VigenciaFinal])}\")."));
                continue;
            }

            // Vigência invertida não é "linha estranha": tornaria a expansão de ocorrências
            // silenciosamente vazia, e a unidade apareceria sem oferta nenhuma sem ninguém saber por quê.
            if (vigFim < vigIni)
            {
                rejeitadas.Add(new(numero, bruta,
                    $"Vigência termina antes de começar ({vigIni:dd/MM/yyyy} → {vigFim:dd/MM/yyyy})."));
                continue;
            }

            var cnes = Digitos(c[CodCnes]);
            if (cnes.Length != 7)
            {
                rejeitadas.Add(new(numero, bruta, $"CNES inválido (\"{Curto(c[CodCnes])}\")."));
                continue;
            }

            var cpf = Digitos(c[CpfProfissional]);
            if (cpf.Length != 11)
            {
                rejeitadas.Add(new(numero, bruta,
                    $"CPF de profissional inválido — a escala é publicada por profissional e sem ele "
                    + "não há como abrir a agenda de ninguém."));
                continue;
            }

            escalas.Add(new LinhaEscala(
                CodigoEscala: codigo,
                Cnes: cnes,
                UnidadeNome: Limpo(c[DescCnes]) ?? cnes,
                ProfissionalCpf: cpf,
                ProfissionalNome: Limpo(c[NomeProfissional]) ?? "(não informado)",
                CboCodigo: Limpo(c[CodCbo]),
                CboDescricao: Limpo(c[DescCbo]),
                ProcedimentoCodigo: Limpo(c[CodProcedimentoInterno]) ?? string.Empty,
                ProcedimentoNome: Limpo(c[DescProcedimentoInterno]) ?? string.Empty,
                ProcedimentoSigtap: Limpo(c[CodProcedimentoUnificado]),
                DiaSemana: dia,
                HoraInicio: horaIni,
                HoraFim: horaFim,
                VagasPrimeiraVez: Inteiro(c[VagasPrimeiraVez]),
                MinutosPrimeiraVez: Inteiro(c[MinutosPrimeiraVez]),
                VagasRetorno: Inteiro(c[VagasRetorno]),
                MinutosRetorno: Inteiro(c[MinutosRetorno]),
                VagasReserva: Inteiro(c[VagasReserva]),
                MinutosReserva: Inteiro(c[MinutosReserva]),
                VigenciaInicio: vigIni,
                VigenciaFim: vigFim,
                Status: status,
                AgendaLocal: Sim(c[AgendaLocal]),
                QuebraAutomatica: Sim(c[QuebraAutomatica]),
                OperadorCriador: Limpo(c[OperadorCriador]),
                OperadorModificador: Limpo(c[OperadorModificador]),
                InseridaEm: Data(c[DataInsercao]),
                AlteradaEm: Data(c[DataUltimaAlteracao]),
                AtivadaEm: Data(c[DataUltimaAtivacao])));
        }

        return new Resultado(escalas, rejeitadas);
    }

    // ---- helpers ----

    private static IEnumerable<string> Linhas(string conteudo)
    {
        foreach (var l in conteudo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var linha = l.TrimEnd();
            if (linha.Length > 0) yield return linha;
        }
    }

    /// <summary>A 1ª célula do cabeçalho de colunas começa com "COD" e não é numérica.</summary>
    private static bool EhCabecalhoColunas(string[] c) =>
        c.Length > 0 && c[0].TrimStart().StartsWith("COD", StringComparison.OrdinalIgnoreCase);

    /// <summary>O SISREG escreve <c>---</c> quando o campo não tem valor — não é conteúdo.</summary>
    private static string? Limpo(string? valor)
    {
        var v = valor?.Trim();
        if (string.IsNullOrEmpty(v) || v == "---" || v.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;
        return v;
    }

    private static string Digitos(string? valor) =>
        new((valor ?? string.Empty).Where(char.IsDigit).ToArray());

    private static bool SoDigitos(string? valor)
    {
        var v = valor?.Trim();
        return !string.IsNullOrEmpty(v) && v.All(char.IsDigit);
    }

    private static int Inteiro(string? valor) =>
        int.TryParse(Limpo(valor), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0 ? n : 0;

    private static bool Sim(string? valor) =>
        string.Equals(Limpo(valor), "SIM", StringComparison.OrdinalIgnoreCase);

    private static DateOnly? Data(string? valor) =>
        DateOnly.TryParseExact(Limpo(valor), "dd/MM/yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d)
            ? d
            : null;

    private static TimeOnly? Hora(string? valor) =>
        TimeOnly.TryParseExact(Limpo(valor), "HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var t)
            ? t
            : null;

    private static string Curto(string? valor)
    {
        var v = (valor ?? string.Empty).Trim();
        return v.Length <= 30 ? v : v[..30] + "…";
    }
}
