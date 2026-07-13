using System.Globalization;
using System.Text.RegularExpressions;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Common.Texto;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Conversas.RespostasRapidas;

/// <summary>Uma tag automática — o operador não digita, ela sai do contexto da conversa.</summary>
/// <param name="Nome">Como aparece no corpo, sem as chaves (ex.: <c>primeironome</c>).</param>
/// <param name="Descricao">Explicação mostrada na tela de cadastro.</param>
public sealed record TagAutomatica(string Nome, string Descricao);

/// <summary>Tudo o que o contexto da conversa sabe oferecer às tags automáticas.</summary>
public sealed record ContextoTags(
    string? PacienteNome,
    string? Cpf,
    string? Cns,
    DateOnly? DataNascimento,
    string? Telefone,
    string? OperadorNome,
    string? UnidadeNome);

/// <summary>
/// Motor das tags <c>{{...}}</c> das respostas rápidas. Duas famílias, resolvidas aqui no
/// servidor para o texto sair pronto: AUTOMÁTICAS (vêm do contexto — paciente da conversa,
/// operador, unidade) e MANUAIS (declaradas na mensagem, preenchidas pelo operador na hora).
///
/// Tag sem valor NÃO some do texto: vira o marcador visível <c>{{nome}}</c> de novo, para o
/// operador enxergar o buraco antes de enviar em vez de mandar uma frase truncada.
/// </summary>
public static partial class TagsRespostaRapida
{
    /// <summary>Catálogo mostrado na tela de cadastro (e o que <see cref="Resolver"/> entende).</summary>
    public static readonly IReadOnlyList<TagAutomatica> Automaticas =
    [
        new("primeironome", "Primeiro nome do paciente da conversa"),
        new("nomecompleto", "Nome completo do paciente"),
        new("cpf", "CPF do paciente"),
        new("cns", "CNS (Cartão SUS) do paciente"),
        new("nascimento", "Data de nascimento do paciente (dd/MM/aaaa)"),
        new("telefone", "Telefone da conversa"),
        new("atendente", "Primeiro nome de quem está atendendo"),
        new("unidade", "Nome da unidade responsável pela conversa"),
        new("saudacao", "Bom dia / Boa tarde / Boa noite, conforme a hora em Maricá"),
    ];

    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}")]
    private static partial Regex Tag();

    /// <summary>Nomes das tags usadas no corpo, na ordem em que aparecem (sem repetição).</summary>
    public static IReadOnlyList<string> Extrair(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo)) return [];

        var vistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return [.. Tag().Matches(corpo)
            .Select(m => m.Groups[1].Value)
            .Where(n => vistas.Add(n))];
    }

    public static bool EhAutomatica(string nome) =>
        Automaticas.Any(t => string.Equals(t.Nome, nome, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Troca as tags pelos valores. <paramref name="valores"/> traz o que o operador digitou
    /// (chave = nome do campo manual). O que não tem valor volta como <c>{{nome}}</c>.
    /// </summary>
    public static string Resolver(
        string corpo,
        ContextoTags contexto,
        IReadOnlyDictionary<string, string> valores,
        IReadOnlyDictionary<string, TipoCampoRespostaRapida> tipos)
    {
        return Tag().Replace(corpo, m =>
        {
            var nome = m.Groups[1].Value;

            var automatico = ResolverAutomatica(nome, contexto);
            if (automatico is not null) return automatico;

            if (valores.TryGetValue(nome, out var valor) && !string.IsNullOrWhiteSpace(valor))
                return Formatar(valor, tipos.GetValueOrDefault(nome, TipoCampoRespostaRapida.Texto));

            return m.Value; // sem valor: o buraco fica visível.
        });
    }

    private static string? ResolverAutomatica(string nome, ContextoTags c) => nome.ToLowerInvariant() switch
    {
        "primeironome" => Vazio(NomePessoa.PrimeiroNome(c.PacienteNome)),
        "nomecompleto" => Vazio(NomePessoa.Capitalizar(c.PacienteNome)),
        "cpf" => Vazio(FormatarCpf(c.Cpf)),
        "cns" => Vazio(c.Cns),
        "nascimento" => c.DataNascimento?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        "telefone" => Vazio(c.Telefone),
        "atendente" => Vazio(NomePessoa.PrimeiroNome(c.OperadorNome)),
        "unidade" => Vazio(c.UnidadeNome),
        "saudacao" => Saudacao(),
        _ => null,
    };

    /// <summary>Tag automática conhecida mas sem dado (paciente sem CNS, p.ex.) — devolve null
    /// para o marcador reaparecer, em vez de deixar um espaço em branco na frase.</summary>
    private static string? Vazio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;

    private static string Saudacao()
    {
        var hora = FusoBrasilia.ParaExibicao(DateTime.UtcNow).Hour;
        return hora < 12 ? "Bom dia" : hora < 18 ? "Boa tarde" : "Boa noite";
    }

    private static string Formatar(string valor, TipoCampoRespostaRapida tipo) => tipo switch
    {
        // O front manda a data como yyyy-MM-dd (input date); o paciente lê dd/MM/aaaa.
        TipoCampoRespostaRapida.Data =>
            DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out var d)
                ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : valor.Trim(),
        _ => valor.Trim(),
    };

    private static string? FormatarCpf(string? cpf)
    {
        var d = new string([.. (cpf ?? "").Where(char.IsDigit)]);
        return d.Length == 11 ? $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}" : cpf;
    }
}
