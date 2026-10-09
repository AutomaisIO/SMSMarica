using System.Globalization;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.EnvioSer;

/// <summary>
/// O painel do paciente da tela de criação preenchido com o NOSSO cadastro — para quando o sistema
/// não conhece o paciente e abre o painel vazio para digitar (o SERNIT, que não consulta o CADSUS).
/// É o que o regulador digitaria, com as máscaras da tela (medidas em 08/10/2026 no SERNIT real).
///
/// <para>Campo pelo RÓTULO: os telefones são <c>j_id</c> posicionais. Só entra campo que a tela
/// tem, aberto e vazio. UF e município não estão aqui — o município depende do <c>onchange</c> da
/// UF, que é requisição; quem faz é o envio.</para>
/// </summary>
public static class PacienteNaTela
{
    /// <summary>Sem estes o pedido não tem paciente identificável — o envio para antes do Gravar.</summary>
    private static readonly string[] Indispensaveis = ["Nome", "CPF", "Sexo", "Data de Nascimento"];

    /// <summary>"Nome da Mãe" tem <c>maxlength=50</c> na tela; o navegador corta calado.</summary>
    private const int TetoNomeMae = 50;

    public sealed record Montagem(
        IReadOnlyList<(string Campo, string Rotulo, string Valor)> Campos,
        IReadOnlyList<string> Faltando,
        IReadOnlyList<string> Avisos);

    public static Montagem Montar(PacienteDto p, IReadOnlyList<SerCampoPacienteDto> painel)
    {
        var avisos = new List<string>();
        var mae = Maiusculo(p.NomeDaMae);
        if (mae is { Length: > TetoNomeMae })
        {
            avisos.Add($"nome da mãe cortado em {TetoNomeMae} letras (limite da tela)");
            mae = mae[..TetoNomeMae].TrimEnd();
        }

        var e = p.Endereco;
        var nossos = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [Chave("Nome")] = Maiusculo(p.NomeCompleto),
            [Chave("CPF")] = Mascara(Digitos(p.Cpf), 11, "{0}.{1}.{2}-{3}", 3, 3, 3, 2),
            [Chave("Sexo")] = p.Sexo switch { Sexo.Masculino => "M", Sexo.Feminino => "F", _ => null },
            [Chave("Data de Nascimento")] = p.DataNascimento?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            [Chave("Nome da Mãe")] = mae,
            [Chave("Logradouro")] = Maiusculo(e?.Logradouro),
            [Chave("Número")] = Vazio(e?.Numero),
            [Chave("Complemento")] = Maiusculo(e?.Complemento),
            [Chave("CEP")] = Mascara(Digitos(e?.Cep), 8, "{0}-{1}", 5, 3),
            [Chave("Bairro")] = Maiusculo(e?.Bairro),
            [Chave("Telefone Celular")] = Celular(p),
            [Chave("Telefone Residencial")] = Telefone(p.TelefoneResidencial, fixo: true),
            [Chave("Raça")] = p.RacaCor switch
            {
                RacaCor.Branca => "BRANCA",
                RacaCor.Preta => "PRETA",
                RacaCor.Parda => "PARDA",
                RacaCor.Amarela => "AMARELA",
                RacaCor.Indigena => "INDIGENA",
                _ => "SEM_INFORMACAO",
            },
        };

        var campos = new List<(string, string, string)>();
        foreach (var c in painel)
        {
            if (!c.Editavel || !string.IsNullOrWhiteSpace(c.Valor)) continue;
            if (nossos.GetValueOrDefault(Chave(c.Rotulo)) is not { Length: > 0 } valor) continue;
            // Select: só valor que a tela oferece (o SERNIT descarta o resto calado).
            if (c.Opcoes is { } opcoes && opcoes.All(o => o.Valor != valor)) continue;
            campos.Add((c.Campo, c.Rotulo, valor));
        }

        var postos = campos.Select(c => Chave(c.Item2)).ToHashSet(StringComparer.Ordinal);
        var faltando = Indispensaveis
            .Where(r => painel.Any(c => Chave(c.Rotulo) == Chave(r) && c.Editavel && string.IsNullOrWhiteSpace(c.Valor))
                        && !postos.Contains(Chave(r)))
            .ToList();
        return new Montagem(campos, faltando, avisos);
    }

    /// <summary>A UF da tela vem por extenso ("RIO DE JANEIRO"); o nosso cadastro guarda a sigla.</summary>
    public static string NomeDaUf(string? uf) =>
        Ufs.GetValueOrDefault((uf ?? string.Empty).Trim().ToUpperInvariant()) ?? uf ?? string.Empty;

    private static readonly Dictionary<string, string> Ufs = new(StringComparer.Ordinal)
    {
        ["AC"] = "ACRE", ["AL"] = "ALAGOAS", ["AM"] = "AMAZONAS", ["AP"] = "AMAPA", ["BA"] = "BAHIA",
        ["CE"] = "CEARA", ["DF"] = "DISTRITO FEDERAL", ["ES"] = "ESPIRITO SANTO", ["GO"] = "GOIAS",
        ["MA"] = "MARANHAO", ["MG"] = "MINAS GERAIS", ["MS"] = "MATO GROSSO DO SUL", ["MT"] = "MATO GROSSO",
        ["PA"] = "PARA", ["PB"] = "PARAIBA", ["PE"] = "PERNAMBUCO", ["PI"] = "PIAUI", ["PR"] = "PARANA",
        ["RJ"] = "RIO DE JANEIRO", ["RN"] = "RIO GRANDE DO NORTE", ["RO"] = "RONDONIA", ["RR"] = "RORAIMA",
        ["RS"] = "RIO GRANDE DO SUL", ["SC"] = "SANTA CATARINA", ["SE"] = "SERGIPE", ["SP"] = "SAO PAULO",
        ["TO"] = "TOCANTINS",
    };

    /// <summary>
    /// O número do campo "Telefone Celular" (o SERNIT exige para gravar): o celular do cadastro, o
    /// principal ou o verificado — o primeiro que é celular com DDD. O número NEGADO ("não sou essa
    /// pessoa", ADR-0057) nunca vai: o SERNIT avisa o paciente por ele.
    /// </summary>
    internal static string? Celular(PacienteDto p)
    {
        var negado = SemPais(Digitos(p.TelefoneNegado));
        return new[] { p.TelefoneCelular, p.TelefonePrincipal, p.TelefoneVerificado }
            .Where(n => negado.Length == 0 || SemPais(Digitos(n)) != negado)
            .Select(n => Telefone(n))
            .FirstOrDefault(n => n is not null);
    }

    /// <summary>Celular <c>(99)99999-9999</c>, fixo <c>(99)9999-9999</c>; o 55 do país sai.</summary>
    internal static string? Telefone(string? numero, bool fixo = false)
    {
        var d = SemPais(Digitos(numero));
        return (fixo, d.Length) switch
        {
            (false, 11) => $"({d[..2]}){d[2..7]}-{d[7..]}",
            (true, 10) => $"({d[..2]}){d[2..6]}-{d[6..]}",
            _ => null,
        };
    }

    private static string? Mascara(string digitos, int tamanho, string formato, params int[] partes)
    {
        if (digitos.Length != tamanho) return null;
        var pedacos = new List<object>();
        var i = 0;
        foreach (var n in partes)
        {
            pedacos.Add(digitos.Substring(i, n));
            i += n;
        }
        return string.Format(CultureInfo.InvariantCulture, formato, [.. pedacos]);
    }

    private static string Chave(string rotulo) => IdentidadePorNome.Chave(rotulo);

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Maiusculo(string? s) => Vazio(s)?.ToUpper(new CultureInfo("pt-BR"));

    private static string Digitos(string? s) => new([.. (s ?? string.Empty).Where(char.IsDigit)]);

    private static string SemPais(string d) =>
        d.Length is 12 or 13 && d.StartsWith("55", StringComparison.Ordinal) ? d[2..] : d;
}
