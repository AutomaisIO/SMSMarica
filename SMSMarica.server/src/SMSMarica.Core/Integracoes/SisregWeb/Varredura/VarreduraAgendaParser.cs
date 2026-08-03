using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura;

/// <summary>
/// Parser da listagem de agenda do SISREG (<c>cons_agendas</c>, <c>etapa=ListaConsulta</c>).
///
/// <para>Cada agendamento é uma <c>&lt;table id="tblConsulta{nº}"&gt;</c> com 3 <c>&lt;tr&gt;</c>, e
/// os valores vêm como <c>&lt;B&gt;Rótulo:&lt;/B&gt;&lt;BR&gt;valor</c> dentro de cada célula.
/// <b>Não há cabeçalho de tabela</b> — a leitura é por rótulo, nunca por índice de coluna: o
/// SISREG muda a ordem das células conforme os checkboxes marcados na busca.</para>
/// </summary>
public static partial class VarreduraAgendaParser
{
    /// <summary>Um agendamento cru da listagem, antes de virar envelope.</summary>
    public sealed record Linha(
        string CoSolicitacao,
        string? Cns,
        string? Paciente,
        string? Nascimento,
        string? Idade,
        string? Origem,
        string? Telefones,
        string? UnidadeSolicitante,
        string? CnesSolicitante,
        string? VagaSolicitada,
        string? VagaConsumida,
        string? Cid10,
        string? Data,
        string? DiaSemana,
        string? Hora,
        string? Situacao,
        string? Procedimentos);

    /// <param name="TotalPaginas">Sempre &gt;= 1. Sem resultados, o bloco de paginação some e vale 1.</param>
    public sealed record Pagina(
        IReadOnlyList<Linha> Linhas,
        int TotalPaginas,
        string? CnesExecutante,
        string? NomeExecutante);

    private static readonly HtmlParser Parser = new();

    public static Pagina Parse(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new Pagina([], 1, null, null);

        var documento = Parser.ParseDocument(html);
        var (cnesExecutante, nomeExecutante) = LerUnidadeExecutante(documento);

        var linhas = new List<Linha>();
        foreach (var tabela in documento.QuerySelectorAll("table[id^='tblConsulta']"))
        {
            if (LerLinha(tabela) is { } linha) linhas.Add(linha);
        }

        return new Pagina(linhas, LerTotalPaginas(documento), cnesExecutante, nomeExecutante);
    }

    private static Linha? LerLinha(IElement tabela)
    {
        var codigo = (tabela.Id ?? string.Empty).Replace("tblConsulta", string.Empty, StringComparison.Ordinal).Trim();
        if (codigo.Length == 0) return null;

        // Um dicionário rótulo→valor da tabela inteira: os 3 <tr> não têm significado próprio, só
        // agrupam visualmente. Ler assim sobrevive a mudança de layout entre versões do SISREG.
        var campos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var celula in tabela.QuerySelectorAll("td"))
        {
            var rotulo = celula.QuerySelector("b")?.TextContent;
            if (string.IsNullOrWhiteSpace(rotulo)) continue;

            var chave = Normalizar(rotulo).TrimEnd(':').Trim();
            if (chave.Length == 0) continue;

            // O valor é o texto da célula menos o rótulo. Pegar o "resto" em vez do próximo nó
            // aguenta o <BR> e as variações de marcação que o SISREG usa entre campos.
            var texto = Normalizar(celula.TextContent);
            var rotuloNormalizado = Normalizar(rotulo);
            var valor = texto.StartsWith(rotuloNormalizado, StringComparison.OrdinalIgnoreCase)
                ? texto[rotuloNormalizado.Length..]
                : texto;

            valor = valor.TrimStart(':', ' ').Trim();

            // "Procedimento(s):" é rótulo sozinho numa célula com rowspan; o valor está na célula
            // seguinte (e pode vir prefixado pela ordem, "01 - NOME"). Sem este salto o campo
            // sairia vazio — e o procedimento é justamente o que decide o tipo de exame.
            if (valor.Length == 0 && rotulo.TrimEnd().EndsWith(':'))
                valor = Normalizar(celula.NextElementSibling?.TextContent ?? string.Empty);

            if (valor.Length > 0 && !campos.ContainsKey(chave)) campos[chave] = valor;
        }

        var (unidadeSolicitante, cnesSolicitante) = SepararCnes(Campo(campos, "Unidade Solicitante"));
        var (data, diaSemana, hora) = SepararDataHora(Campo(campos, "Data/Hora"));

        return new Linha(
            CoSolicitacao: codigo,
            Cns: Campo(campos, "CNS"),
            Paciente: Campo(campos, "Paciente"),
            Nascimento: Campo(campos, "Nascimento"),
            Idade: Campo(campos, "Idade"),
            Origem: Campo(campos, "Origem"),
            Telefones: Campo(campos, "Telefone(s)") ?? Campo(campos, "Telefone"),
            UnidadeSolicitante: unidadeSolicitante,
            CnesSolicitante: cnesSolicitante,
            VagaSolicitada: Campo(campos, "Vaga Solicitada"),
            VagaConsumida: Campo(campos, "Vaga Consumida"),
            Cid10: Campo(campos, "CID-10"),
            Data: data,
            DiaSemana: diaSemana,
            Hora: hora,
            Situacao: Campo(campos, "Situação"),
            Procedimentos: Campo(campos, "Procedimento(s)") ?? Campo(campos, "Procedimento"));
    }

    /// <summary>
    /// "Mostrando Página [input] de N". O <c>&lt;input&gt;</c> não contribui texto, então o texto
    /// achatado vira literalmente "Mostrando Página de N" — daí o "de" opcional no meio.
    /// </summary>
    private static int LerTotalPaginas(IDocument documento)
    {
        var texto = Normalizar(documento.Body?.TextContent ?? string.Empty);

        var m = TotalPaginas().Match(texto);
        return m.Success && int.TryParse(m.Groups[1].Value, out var total) && total > 0 ? total : 1;
    }

    /// <summary>Bloco "Propriedades da Agenda": confirma de qual unidade é a agenda que voltou.</summary>
    private static (string? Cnes, string? Nome) LerUnidadeExecutante(IDocument documento)
    {
        foreach (var celula in documento.QuerySelectorAll("td"))
        {
            if (!Normalizar(celula.TextContent).StartsWith("Unidade Executante", StringComparison.OrdinalIgnoreCase))
                continue;

            // O valor mora na célula seguinte (é layout de tabela rótulo | valor, não <B> inline).
            var valor = Normalizar(celula.NextElementSibling?.TextContent ?? string.Empty);
            if (valor.Length == 0) continue;

            var (nome, cnes) = SepararCnes(valor);
            return (cnes, nome);
        }

        return (null, null);
    }

    /// <summary><c>"USF JOSEFA XAVIER LEAL (1234567)"</c> → nome e CNES.</summary>
    internal static (string? Nome, string? Cnes) SepararCnes(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return (null, null);

        var m = CnesNoFim().Match(valor);
        return m.Success
            ? (m.Groups[1].Value.Trim(), m.Groups[2].Value)
            : (valor.Trim(), null);
    }

    /// <summary><c>"28/07/2026 - TER - 08:00"</c> → data, dia da semana e hora.</summary>
    internal static (string? Data, string? DiaSemana, string? Hora) SepararDataHora(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return (null, null, null);

        var partes = valor.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return partes.Length switch
        {
            0 => (null, null, null),
            1 => (partes[0], null, null),
            2 => (partes[0], null, partes[1]),
            _ => (partes[0], partes[1], partes[2]),
        };
    }

    private static string? Campo(Dictionary<string, string> campos, string chave) =>
        campos.TryGetValue(chave, out var valor) && valor.Length > 0 ? valor : null;

    /// <summary>
    /// Colapsa espaço e normaliza o NBSP. O SISREG usa <c>&amp;nbsp;</c> dentro de valores
    /// ("Pendente&amp;nbsp;Confirmação"), e sem trocar por espaço comum a comparação de rótulo
    /// falha de um jeito difícil de enxergar — os textos parecem idênticos na tela.
    /// </summary>
    private static string Normalizar(string valor) =>
        EspacosRepetidos().Replace(valor.Replace(' ', ' '), " ").Trim();

    [GeneratedRegex(@"Mostrando\s+P.gina\s*(?:de\s*)?(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex TotalPaginas();

    [GeneratedRegex(@"^(.*?)\s*\((\d{4,10})\)\s*$")]
    private static partial Regex CnesNoFim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRepetidos();
}
