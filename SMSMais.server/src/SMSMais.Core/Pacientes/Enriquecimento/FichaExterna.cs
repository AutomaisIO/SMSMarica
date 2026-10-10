using System.Globalization;
using System.Text;
using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Pacientes.Enriquecimento;

/// <summary>Telefone como veio da fonte: só dígitos (DDD + número), tipo do nosso telecom e o rótulo de lá.</summary>
internal sealed record TelefoneExterno(string Numero, string Tipo, string Rotulo);

/// <summary>
/// O cadastro do cidadão numa fonte de fora (CADSUS pelo SER, ou e-SUS PEC), no vocabulário da nossa
/// ficha. Campo que a fonte não tem fica null — e null nunca vira proposta de apagar nada.
/// </summary>
internal sealed record FichaExterna(
    string Fonte,
    string? Cpf,
    string? Cns,
    string? Nome,
    string? NomeSocial,
    DateOnly? DataNascimento,
    Sexo? Sexo,
    string? NomeMae,
    string? NomePai,
    RacaCor? RacaCor,
    string? Email,
    EnderecoDto? Endereco,
    IReadOnlyList<TelefoneExterno> Telefones,
    IReadOnlyList<string> Avisos,
    DateTime? AtualizadoEm)
{
    public const string Cadsus = "cadsus";
    public const string Esus = "esus";

    public static string Rotulo(string fonte) => fonte == Esus ? "e-SUS" : "CADSUS";

    /// <summary>Origem gravada no telefone acrescentado (<c>contato-origem</c>). A do e-SUS é a mesma
    /// da rotina noturna (ADR-0067).</summary>
    public static string OrigemTelefone(string fonte) => fonte == Esus ? TrocaTelefoneEsus.Origem : "cadsus";

    /// <summary>
    /// O painel de paciente do SER (CADSUS) — <c>form0:painelDadosDoPaciente</c>. Ids medidos na
    /// captura de 10/08/2026 (nome, cpf, cns, nomeSocial, dataNascimento, sexo, nomeMae, logradouro,
    /// numero, complemento, cep, uf, municipio, bairro, raca, telefoneContato); os dois outros
    /// telefones têm id POSICIONAL (j_id173/178), por isso telefone e, na falta do id, qualquer campo
    /// são achados pelo rótulo. O SER não tem nome do pai nem e-mail.
    /// </summary>
    public static FichaExterna DoSer(IReadOnlyList<SerCampoPacienteDto> campos, IReadOnlyList<string> avisos)
    {
        SerCampoPacienteDto? Campo(string id, string rotulo) =>
            campos.FirstOrDefault(c => string.Equals(c.Campo, id, StringComparison.Ordinal))
            ?? campos.FirstOrDefault(c => NormaFicha.Rotulo(c.Rotulo) == NormaFicha.Rotulo(rotulo));

        string? Texto(string id, string rotulo) => NormaFicha.Vazio(Campo(id, rotulo)?.Valor);

        // Select: o value é código (UF e município são números; sexo é a inicial) — o texto é o da opção.
        string? Opcao(string id, string rotulo)
        {
            var c = Campo(id, rotulo);
            if (c is null || string.IsNullOrWhiteSpace(c.Valor)) return null;
            var texto = c.Opcoes?.FirstOrDefault(o => string.Equals(o.Valor, c.Valor, StringComparison.OrdinalIgnoreCase))?.Rotulo;
            return NormaFicha.Vazio(texto) ?? NormaFicha.Vazio(c.Valor);
        }

        var sexoCampo = Campo("form0:sexo", "Sexo");
        var telefones = campos
            .Where(c => NormaFicha.Rotulo(c.Rotulo).StartsWith("telefone", StringComparison.Ordinal))
            .Select(c => (Numero: NormaFicha.Telefone(c.Valor), c.Rotulo))
            .Where(t => t.Numero is not null)
            .Select(t => new TelefoneExterno(t.Numero!, TipoPeloRotulo(t.Rotulo, t.Numero!), t.Rotulo.Replace("*", "").Trim()))
            .ToList();

        return new FichaExterna(
            Cadsus,
            Cpf: NormaFicha.Digitos(Texto("form0:cpf", "CPF")) is { Length: 11 } cpf ? cpf : null,
            Cns: NormaFicha.Digitos(Texto("form0:cns", "CNS")) is { Length: 15 } cns ? cns : null,
            Nome: Texto("form0:nome", "Nome"),
            NomeSocial: Texto("form0:nomeSocial", "Nome Social"),
            DataNascimento: NormaFicha.ParaData(Texto("form0:dataNascimento", "Data de Nascimento")),
            Sexo: NormaFicha.ParaSexo(sexoCampo?.Valor) ?? NormaFicha.ParaSexo(Opcao("form0:sexo", "Sexo")),
            NomeMae: Texto("form0:nomeMae", "Nome da Mãe"),
            NomePai: null,
            RacaCor: NormaFicha.ParaRaca(Opcao("form0:raca", "Raça")),
            Email: null,
            Endereco: NormaFicha.Endereco(
                Texto("form0:cep", "CEP"), Texto("form0:logradouro", "Logradouro"), Texto("form0:numero", "Número"),
                Texto("form0:complemento", "Complemento"), Texto("form0:bairro", "Bairro"),
                Opcao("form0:municipio", "Município"), Opcao("form0:uf", "UF")),
            Telefones: telefones,
            Avisos: avisos,
            AtualizadoEm: null);
    }

    /// <summary>A ficha do cidadão no e-SUS PEC (<c>BuscaDetailCidadao</c>).</summary>
    public static FichaExterna DoEsus(FichaCidadaoEsusPec f)
    {
        var avisos = new List<string>();
        if (f.Faleceu) avisos.Add("O e-SUS registra o óbito deste cidadão.");
        if (!f.Ativo) avisos.Add("O cadastro deste cidadão está inativo no e-SUS.");

        var telefones = new List<TelefoneExterno>();
        void Tel(string? valor, string rotulo, string? tipo)
        {
            if (NormaFicha.Telefone(valor) is { } n && !telefones.Any(t => NormaFicha.MesmoNumero(t.Numero, n)))
                telefones.Add(new TelefoneExterno(n, tipo ?? TipoPeloNumero(n), rotulo));
        }
        Tel(f.TelefoneCelular, "Celular", "celular");
        Tel(f.TelefoneResidencial, "Residencial", "residencial");
        Tel(f.TelefoneContato, "Contato", null);

        // O PEC separa o tipo ("AVENIDA") do nome; a nossa ficha guarda os dois juntos.
        var logradouro = NormaFicha.Vazio(f.Logradouro) is { } l
            ? (NormaFicha.Vazio(f.TipoLogradouro) is { } tipo ? $"{tipo} {l}" : l)
            : null;

        return new FichaExterna(
            Esus,
            Cpf: NormaFicha.Digitos(f.Cpf) is { Length: 11 } cpf ? cpf : null,
            Cns: NormaFicha.Digitos(f.Cns) is { Length: 15 } cns ? cns : null,
            Nome: NormaFicha.Vazio(f.Nome),
            NomeSocial: NormaFicha.Vazio(f.NomeSocial),
            DataNascimento: NormaFicha.ParaData(f.DataNascimento),
            Sexo: NormaFicha.ParaSexo(f.Sexo),
            NomeMae: NormaFicha.Vazio(f.NomeMae),
            NomePai: NormaFicha.Vazio(f.NomePai),
            RacaCor: NormaFicha.ParaRaca(f.RacaCor),
            Email: NormaFicha.Vazio(f.Email)?.ToLowerInvariant(),
            Endereco: NormaFicha.Endereco(f.Cep, logradouro, f.Numero, f.Complemento, f.Bairro, f.Municipio, f.Uf),
            Telefones: telefones,
            Avisos: avisos,
            AtualizadoEm: f.AtualizadoEm);
    }

    private static string TipoPeloRotulo(string rotulo, string numero)
    {
        var r = NormaFicha.Rotulo(rotulo);
        if (r.Contains("residencial", StringComparison.Ordinal)) return "residencial";
        if (r.Contains("whatsapp", StringComparison.Ordinal) || r.Contains("celular", StringComparison.Ordinal)) return "celular";
        return TipoPeloNumero(numero);
    }

    private static string TipoPeloNumero(string numero) =>
        numero.Length == 11 && numero[2] == '9' ? "celular" : "residencial";
}

/// <summary>Normalização comum às duas fontes e à ficha — é o que decide "igual" × "diferente".</summary>
internal static class NormaFicha
{
    private static readonly Dictionary<string, string> Ufs = new(StringComparer.Ordinal)
    {
        ["ACRE"] = "AC", ["ALAGOAS"] = "AL", ["AMAPA"] = "AP", ["AMAZONAS"] = "AM", ["BAHIA"] = "BA",
        ["CEARA"] = "CE", ["DISTRITO FEDERAL"] = "DF", ["ESPIRITO SANTO"] = "ES", ["GOIAS"] = "GO",
        ["MARANHAO"] = "MA", ["MATO GROSSO"] = "MT", ["MATO GROSSO DO SUL"] = "MS", ["MINAS GERAIS"] = "MG",
        ["PARA"] = "PA", ["PARAIBA"] = "PB", ["PARANA"] = "PR", ["PERNAMBUCO"] = "PE", ["PIAUI"] = "PI",
        ["RIO DE JANEIRO"] = "RJ", ["RIO GRANDE DO NORTE"] = "RN", ["RIO GRANDE DO SUL"] = "RS",
        ["RONDONIA"] = "RO", ["RORAIMA"] = "RR", ["SANTA CATARINA"] = "SC", ["SAO PAULO"] = "SP",
        ["SERGIPE"] = "SE", ["TOCANTINS"] = "TO",
    };

    public static string? Vazio(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public static string Digitos(string? v) => string.IsNullOrEmpty(v) ? string.Empty : new([.. v.Where(char.IsDigit)]);

    /// <summary>Sem acento, maiúsculas, espaços colapsados — a régua do "Verificar nome".</summary>
    public static string Texto(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return string.Empty;
        var sb = new StringBuilder(v.Length);
        foreach (var ch in v.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return string.Join(' ', sb.ToString().ToUpperInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Rótulo do SER sem asterisco nem acento, minúsculo ("Nome da Mãe*" → "nome da mae").</summary>
    public static string Rotulo(string? v) => Texto(v?.Replace("*", " ")).ToLowerInvariant();

    public static DateOnly? ParaData(string? v)
    {
        var t = (v ?? string.Empty).Trim();
        if (t.Length > 10 && t[10] is 'T' or ' ') t = t[..10];
        foreach (var formato in (string[])["yyyy-MM-dd", "dd/MM/yyyy", "dd.MM.yyyy"])
            if (DateOnly.TryParseExact(t, formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        return null;
    }

    public static Sexo? ParaSexo(string? v)
    {
        var t = Texto(v);
        if (t.StartsWith('F')) return Sexo.Feminino;
        if (t.StartsWith('M')) return Sexo.Masculino;
        return null;
    }

    public static RacaCor? ParaRaca(string? v)
    {
        var t = Texto(v);
        if (t.StartsWith("BRANC", StringComparison.Ordinal)) return RacaCor.Branca;
        if (t.StartsWith("PRET", StringComparison.Ordinal)) return RacaCor.Preta;
        if (t.StartsWith("PARD", StringComparison.Ordinal)) return RacaCor.Parda;
        if (t.StartsWith("AMAREL", StringComparison.Ordinal)) return RacaCor.Amarela;
        if (t.StartsWith("INDIGEN", StringComparison.Ordinal)) return RacaCor.Indigena;
        return null;
    }

    /// <summary>Sigla da UF a partir da sigla ou do nome por extenso (o PEC manda "RIO DE JANEIRO").</summary>
    public static string? ParaUf(string? v)
    {
        var t = Texto(v);
        if (t.Length == 2 && Ufs.ContainsValue(t)) return t;
        return Ufs.GetValueOrDefault(t);
    }

    /// <summary>Telefone brasileiro em forma nacional (DDD + número, 10 ou 11 dígitos); null se não for
    /// um — placeholder de recepção (99999-9999) também não é.</summary>
    public static string? Telefone(string? v)
    {
        var d = Digitos(v);
        if (d.Length is 12 or 13 && d.StartsWith("55", StringComparison.Ordinal)) d = d[2..];
        if (d.Length is not (10 or 11)) return null;
        if (int.Parse(d[..2], CultureInfo.InvariantCulture) < 11) return null;
        if (d[2..].Distinct().Count() == 1) return null;
        return d;
    }

    /// <summary>Mesmo número tolerando DDI/DDD faltando (um é sufixo do outro), com guarda de tamanho.</summary>
    public static bool MesmoNumero(string? a, string? b)
    {
        var x = Digitos(a);
        var y = Digitos(b);
        return x == y
            || (x.Length >= 8 && y.Length >= 8
                && (x.EndsWith(y, StringComparison.Ordinal) || y.EndsWith(x, StringComparison.Ordinal)));
    }

    public static EnderecoDto? Endereco(
        string? cep, string? logradouro, string? numero, string? complemento, string? bairro, string? cidade, string? uf)
    {
        // Sem logradouro não é endereço que valha propor — seria trocar um endereço por pedaços dele.
        if (Vazio(logradouro) is null) return null;
        var cepDigitos = Digitos(cep);
        return new EnderecoDto(
            cepDigitos.Length == 8 ? $"{cepDigitos[..5]}-{cepDigitos[5..]}" : cepDigitos,
            Vazio(logradouro)!,
            Vazio(numero),
            Vazio(complemento),
            Vazio(bairro) ?? string.Empty,
            Vazio(cidade) ?? string.Empty,
            ParaUf(uf) ?? Vazio(uf) ?? string.Empty,
            null);
    }

    /// <summary>Chave de comparação do endereço. Complemento e ponto de referência ficam de fora: diferença
    /// só neles é ruído, e trocar o endereço inteiro por causa disso não vale a pergunta.</summary>
    public static string ChaveEndereco(EnderecoDto? e) => e is null || Vazio(e.Logradouro) is null
        ? string.Empty
        : string.Join('|', Texto(e.Logradouro), Texto(e.Numero), Texto(e.Bairro), Texto(e.Cidade),
            ParaUf(e.Uf) ?? Texto(e.Uf), Digitos(e.Cep));

    public static string FormatarEndereco(EnderecoDto e)
    {
        var sb = new StringBuilder(e.Logradouro.Trim());
        if (Vazio(e.Numero) is { } n) sb.Append(", ").Append(n);
        if (Vazio(e.Complemento) is { } c) sb.Append(" - ").Append(c);
        if (Vazio(e.Bairro) is { } b) sb.Append(", ").Append(b);
        var cidade = string.Join('/', new[] { Vazio(e.Cidade), Vazio(e.Uf) }.Where(x => x is not null));
        if (cidade.Length > 0) sb.Append(", ").Append(cidade);
        if (Vazio(e.Cep) is { } cep) sb.Append(" · CEP ").Append(cep);
        return sb.ToString();
    }

    public static string FormatarCpf(string cpf) =>
        cpf.Length == 11 ? $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}" : cpf;

    public static string FormatarCns(string cns) =>
        cns.Length == 15 ? $"{cns[..3]} {cns[3..7]} {cns[7..11]} {cns[11..]}" : cns;

    public static string FormatarTelefone(string d) => d.Length switch
    {
        11 => $"({d[..2]}) {d[2..7]}-{d[7..]}",
        10 => $"({d[..2]}) {d[2..6]}-{d[6..]}",
        _ => d,
    };
}
