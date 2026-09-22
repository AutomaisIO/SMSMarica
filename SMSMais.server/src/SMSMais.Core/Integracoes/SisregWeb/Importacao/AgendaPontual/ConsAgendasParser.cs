using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao.AgendaPontual;

/// <summary>
/// Lê o RESULTADO da tela "Consulta de Agendas" (<c>cons_agendas</c>, <c>etapa=ListaConsulta</c>)
/// e devolve <see cref="MarcacaoSisreg"/> por agendamento.
///
/// <para><b>Por que esta tela e não a exportação:</b> o <c>expo_solicitacoes</c> (a fonte da
/// varredura noturna) é bloqueado pelo SISREG das 8h às 15h. O botão "Importar" é <b>pontual</b> —
/// serve para o momento em que não dá para esperar as 15h — então usa a tela de agenda, que é
/// paginada mas não tem trava de horário. Escopado a UM par profissional × procedimento e a um
/// intervalo curto (o dia), custa ~1 requisição por página.</para>
///
/// <para><b>Diferença para o TXT/CSV:</b> o <c>cons_agendas</c> NÃO informa o código SIGTAP nem o
/// <c>pa</c> por linha — só o NOME do procedimento (ex.: <c>USG OBSTETRICA</c>). A resolução do
/// SIGTAP acontece depois, por nome, no <see cref="ImportacaoAgendaPontualService"/>. Traz
/// nascimento do paciente (o TXT não traz), o que ajuda a criar paciente novo.</para>
///
/// <para>Anatomia: cada agendamento é uma <c>&lt;table id="tblConsulta&lt;codigo&gt;"&gt;</c> com
/// células rotuladas (<c>CNS:</c>, <c>Paciente:</c>, <c>Data/Hora:</c>, <c>Procedimento(s):</c>…).</para>
/// </summary>
public static partial class ConsAgendasParser
{
    /// <summary>Contexto do par consultado — vira proveniência de cada marcação.</summary>
    public sealed record Contexto(
        string CnesExecutante, string? NomeExecutante, string CpfProfissional, string? NomeProfissional);

    /// <summary>Texto que o SISREG devolve quando a consulta não achou nada.</summary>
    public static bool SemResultado(string html) =>
        html.Contains("não retornou nenhum resultado", StringComparison.OrdinalIgnoreCase)
        || html.Contains("nao retornou nenhum resultado", StringComparison.OrdinalIgnoreCase)
        || html.Contains("n&atilde;o retornou nenhum resultado", StringComparison.OrdinalIgnoreCase);

    /// <summary>"Mostrando Página [n] de N" → N (total de páginas). 1 se não achar.</summary>
    public static int TotalPaginas(string html)
    {
        var plano = EspacosRegex().Replace(html, " ");
        var m = TotalPaginasRegex().Match(plano);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) && n >= 1 ? n : 1;
    }

    public static IReadOnlyList<MarcacaoSisreg> Parse(string html, Contexto ctx)
    {
        var doc = new HtmlParser().ParseDocument(html);
        var saida = new List<MarcacaoSisreg>();

        foreach (var tabela in doc.QuerySelectorAll("table[id^='tblConsulta']"))
        {
            var codigo = (tabela.Id ?? string.Empty).Replace("tblConsulta", string.Empty).Trim();
            if (codigo.Length == 0 || !codigo.All(char.IsDigit)) continue;

            string? cns = null, paciente = null, telefone = null, cid = null,
                    unidadeSolic = null, cnesSolic = null, situacao = null,
                    vagaConsumida = null, vagaSolicitada = null, procedimento = null;
            DateOnly? nascimento = null;
            DateTime? dataHora = null;

            foreach (var celula in tabela.QuerySelectorAll("td, th"))
            {
                var texto = EspacosRegex().Replace(celula.TextContent, " ").Trim();
                if (texto.Length == 0) continue;

                // Célula de procedimento: "01 - USG MORFOLOGICO" (sem rótulo com ":").
                var proc = ProcedimentoRegex().Match(texto);
                if (proc.Success)
                {
                    // Primeiro procedimento do agendamento é o que vale (na prática há só um por
                    // linha quando se varre por um procedimento/grupo específico).
                    procedimento ??= proc.Groups[1].Value.Trim();
                    continue;
                }

                var sep = texto.IndexOf(':');
                if (sep < 0) continue;
                var rotulo = Normalizar(texto[..sep]);
                var valor = texto[(sep + 1)..].Trim();
                if (valor.Length == 0) continue;

                switch (rotulo)
                {
                    case var r when r.StartsWith("CNS"): cns = SoDigitos(valor) is { Length: 15 } c ? c : null; break;
                    case var r when r.StartsWith("PACIENTE"): paciente = valor; break;
                    case var r when r.StartsWith("NASCIMENTO"): nascimento = Data(valor); break;
                    case var r when r.StartsWith("TELEFONE"): telefone = valor; break;
                    case var r when r.StartsWith("UNIDADE SOLICITANTE"): (unidadeSolic, cnesSolic) = Unidade(valor); break;
                    case var r when r.StartsWith("CID"): cid = valor; break;
                    case var r when r.StartsWith("DATA/HORA"): dataHora = DataHora(valor); break;
                    case var r when r.StartsWith("SITUA"): situacao = valor; break;
                    case var r when r.StartsWith("VAGA CONSUMIDA"): vagaConsumida = valor; break;
                    case var r when r.StartsWith("VAGA SOLICITADA"): vagaSolicitada = valor; break;
                }
            }

            // Proveniência: envelope JSON do registro observado. Também serve de marcador de "veio
            // do SISREG" (RawSisreg) — é o que distingue uma solicitação importada da MANUAL na
            // reconciliação por número.
            var raw = JsonSerializer.Serialize(new
            {
                origem = "cons_agendas",
                codigo,
                cns,
                paciente,
                procedimento,
                dataHora = dataHora?.ToString("s", CultureInfo.InvariantCulture),
                situacao,
                cnesSolicitante = cnesSolic,
                unidadeSolicitante = unidadeSolic,
                cid,
            });

            saida.Add(new MarcacaoSisreg(
                CodigoSolicitacao: codigo,
                CnsPaciente: cns,
                NomePaciente: paciente,
                ProcedimentoTexto: procedimento,
                // cons_agendas NÃO traz SIGTAP nem pa — resolvidos depois, por nome.
                CodigoSigtap: null,
                CpfMedicoSolicitante: null,
                NomeMedicoSolicitante: null,
                CrmMedicoSolicitante: null,
                CnesUnidadeSolicitante: cnesSolic,
                NomeUnidadeSolicitante: unidadeSolic,
                CnesUnidadeExecutante: ctx.CnesExecutante,
                NomeUnidadeExecutante: ctx.NomeExecutante,
                DataHoraAtendimento: dataHora,
                DataSolicitacao: null,
                DataRegulacao: null,
                Cid: cid,
                TelefonePaciente: telefone,
                LinhaRaw: raw,
                NascimentoPaciente: nascimento,
                SituacaoAgendamento: situacao,
                VagaSolicitada: vagaSolicitada,
                VagaConsumida: vagaConsumida,
                EhRetorno: RetornoDeTexto(vagaSolicitada ?? vagaConsumida),
                CpfProfissionalExecutante: ctx.CpfProfissional,
                NomeProfissionalExecutante: ctx.NomeProfissional,
                CodigoProcedimentoSisreg: null));
        }

        return saida;
    }

    /// <summary>"UNIDADE X (6289851)" → (nome, cnes). Sem parênteses, devolve o nome inteiro.</summary>
    private static (string? Nome, string? Cnes) Unidade(string valor)
    {
        var m = CnesEntreParensRegex().Match(valor);
        if (!m.Success) return (valor, null);
        var nome = valor[..m.Index].Trim();
        return (nome.Length == 0 ? valor : nome, m.Groups[1].Value);
    }

    /// <summary>"26/08/2026 - QUA - 13:00" → DateTime local (Unspecified), como o AgendaTxtParser.</summary>
    private static DateTime? DataHora(string valor)
    {
        var partes = valor.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0) return null;
        var d = Data(partes[0]);
        if (d is null) return null;
        var hora = partes.Length >= 3 ? partes[2] : partes.Length == 2 ? partes[1] : "00:00";
        if (!TimeOnly.TryParseExact(hora, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            t = TimeOnly.MinValue;
        return DateTime.SpecifyKind(d.Value.ToDateTime(t), DateTimeKind.Unspecified);
    }

    private static DateOnly? Data(string? s)
    {
        var t = (s ?? string.Empty).Trim();
        return DateOnly.TryParseExact(t, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;
    }

    private static string SoDigitos(string valor) => new([.. valor.Where(char.IsDigit)]);

    /// <summary>Texto da vaga da tela (<c>RETORNO</c> / <c>1ª VEZ</c> / <c>RESERVA</c>) → retorno?
    /// RETORNO → true; contém "VEZ" (1ª vez) → false; RESERVA/vazio → null (indeterminado).</summary>
    private static bool? RetornoDeTexto(string? valor)
    {
        var v = Normalizar(valor ?? string.Empty);
        if (v.Length == 0) return null;
        if (v.Contains("RETORNO")) return true;
        if (v.Contains("VEZ")) return false;
        return null;
    }

    /// <summary>Maiúsculas sem acento — os rótulos do SISREG oscilam em acentuação entre telas.</summary>
    private static string Normalizar(string s)
    {
        var semAcento = new string([.. s.Normalize(NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
        return semAcento.ToUpperInvariant().Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRegex();

    [GeneratedRegex(@"^\d{1,3}\s*-\s*(.+)$")]
    private static partial Regex ProcedimentoRegex();

    [GeneratedRegex(@"\((\d{6,7})\)\s*$")]
    private static partial Regex CnesEntreParensRegex();

    [GeneratedRegex(@"Mostrando\s+P.gina\s*(?:\[?\s*\d*\s*\]?\s*)?de\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TotalPaginasRegex();
}
