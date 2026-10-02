namespace SMSMais.Core.Regulacao.Medicos;

/// <summary>Um nome de médico como apareceu numa fonte, e quantas vezes.</summary>
/// <param name="Quantidade">Pedidos com este nome. Executante sem pedido entra com 0.</param>
public sealed record OcorrenciaMedico(
    string Nome, string? Cpf, string? Conselho, string? NumeroConselho, string? UfConselho, int Quantidade);

/// <summary>Um médico depois de juntadas as grafias.</summary>
/// <param name="Chave">O nome normalizado da forma mais completa — a chave contra duplicação.</param>
/// <param name="Chaves">Todas as formas normalizadas que caíram neste médico (inclui <paramref name="Chave"/>).</param>
public sealed record MedicoConsolidado(
    string Nome,
    string Chave,
    IReadOnlyList<string> Chaves,
    string? Cpf,
    string? Conselho,
    string? NumeroConselho,
    string? UfConselho,
    int Ocorrencias);

/// <summary>
/// Junta os nomes de médico das fichas do SISREG num cadastro sem duplicação.
///
/// <para><b>O problema:</b> o SISREG não tem cadastro de médico solicitante — cada ficha traz o nome
/// digitado. Em 02/10/2026 eram 15.481 nomes, 13.744 depois de tirar acento e pontuação, e o mesmo
/// médico aparecia como "OTAVIO FRANCISCO SANTOS", "OTAVIO F. SANTOS" e "OTAVIO FRANCICO SANTOS".</para>
///
/// <para><b>Só junta o que é seguro</b> — juntar dois médicos diferentes é pior que deixar um
/// repetido, porque o repetido a pessoa vê e junta com um botão:</para>
/// <list type="bullet">
///   <item>a mesma grafia depois de normalizar (acento, pontuação, DE/DA/DOS);</item>
///   <item>o mesmo CPF válido, se o primeiro nome também bater (CPF de outro médico digitado
///   por engano não arrasta um nome que não tem nada a ver);</item>
///   <item>a variante de UM só nome mais completo: 3+ palavras, todas casando em ordem, primeiro
///   e último nome por extenso ("OTAVIO F SANTOS" → "OTAVIO FRANCISCO SANTOS"); uma letra
///   errada só em palavra de 7+ letras ("FRANCICO", mas não "MARTA" × "MARIA"). Se casa com dois
///   nomes diferentes, não se sabe qual — fica para a pessoa.</item>
/// </list>
/// <para>Dois grupos com CPFs diferentes nunca se juntam: são duas pessoas.</para>
/// </summary>
public static class ConsolidacaoMedicos
{
    /// <summary>A chave normalizada de um nome ("Dr. José da Silva" → "JOSE SILVA").</summary>
    public static string Chave(string? nome) => string.Join(' ', SemelhancaNome.Palavras(nome));

    /// <summary>Nome com cara de nome: duas palavras de verdade ao menos.</summary>
    public static bool NomeAproveitavel(string? nome)
    {
        var p = SemelhancaNome.Palavras(nome);
        return p.Count >= 2 && p.Count(x => x.Length > 1) >= 2;
    }

    public static IReadOnlyList<MedicoConsolidado> Consolidar(IEnumerable<OcorrenciaMedico> ocorrencias)
    {
        // 1. Por grafia normalizada.
        var porChave = new Dictionary<string, Acumulado>(StringComparer.Ordinal);
        foreach (var o in ocorrencias)
        {
            if (!NomeAproveitavel(o.Nome)) continue;
            var chave = Chave(o.Nome);
            if (!porChave.TryGetValue(chave, out var a)) porChave[chave] = a = new Acumulado(chave);
            a.Total += o.Quantidade;
            Somar(a.Grafias, Limpar(o.Nome), Math.Max(o.Quantidade, 1));
            var cpf = CpfValido(o.Cpf);
            if (cpf is not null) Somar(a.Cpfs, cpf, Math.Max(o.Quantidade, 1));
            var numero = (o.NumeroConselho ?? string.Empty).Trim();
            if (numero.Any(char.IsDigit))
            {
                var conselho = string.IsNullOrWhiteSpace(o.Conselho) ? "CRM" : o.Conselho.Trim().ToUpperInvariant();
                var uf = string.IsNullOrWhiteSpace(o.UfConselho) ? string.Empty : o.UfConselho.Trim().ToUpperInvariant();
                Somar(a.Conselhos, $"{conselho}|{numero}|{uf}", Math.Max(o.Quantidade, 1));
            }
        }

        var chaves = porChave.Keys.ToList();
        var pai = chaves.ToDictionary(k => k, k => k, StringComparer.Ordinal);
        var cpfsDoGrupo = chaves.ToDictionary(k => k, k => new HashSet<string>(porChave[k].Cpfs.Keys), StringComparer.Ordinal);

        string Raiz(string x)
        {
            while (pai[x] != x) x = pai[x] = pai[pai[x]];
            return x;
        }

        void Juntar(string a, string b)
        {
            var ra = Raiz(a);
            var rb = Raiz(b);
            if (ra == rb) return;
            var ca = cpfsDoGrupo[ra];
            var cb = cpfsDoGrupo[rb];
            // CPFs diferentes dos dois lados: duas pessoas, mesmo com nome parecido.
            if (ca.Count > 0 && cb.Count > 0 && !ca.Overlaps(cb)) return;
            pai[rb] = ra;
            ca.UnionWith(cb);
        }

        // 2. Mesmo CPF, se o primeiro nome bate com o do nome mais usado daquele CPF.
        foreach (var grupo in chaves
                     .SelectMany(k => porChave[k].Cpfs.Keys.Select(c => (Cpf: c, Chave: k)))
                     .GroupBy(x => x.Cpf))
        {
            var doCpf = grupo.Select(x => x.Chave).OrderByDescending(k => porChave[k].Total).ToList();
            var primeiro = doCpf[0].Split(' ')[0];
            foreach (var k in doCpf.Skip(1))
            {
                if (k.Split(' ')[0] == primeiro) Juntar(doCpf[0], k);
            }
        }

        // 3. Variante de um único nome mais completo, dentro do mesmo primeiro nome.
        // Primeiro as do mesmo tamanho (erro de digitação: "FRANCICO" = "FRANCISCO"), depois as
        // abreviações — contando os candidatos já juntados como um só: "OTAVIO F SANTOS" casa
        // com "FRANCISCO" e com "FRANCICO", mas os dois já são o mesmo médico.
        var blocos = chaves.GroupBy(k => k.Split(' ')[0]).Select(b => b.ToList()).ToList();
        foreach (var lista in blocos)
        {
            foreach (var a in lista.Where(SemIniciais))
            {
                var n = a.Split(' ').Length;
                var candidatos = lista
                    .Where(c => c != a && SemIniciais(c) && c.Split(' ').Length == n && Variante(a, c))
                    .Take(2)
                    .ToList();
                if (candidatos.Count == 1) Juntar(candidatos[0], a);
            }
        }
        foreach (var lista in blocos)
        {
            foreach (var a in lista)
            {
                var candidatos = lista
                    .Where(c => c != a && Raiz(c) != Raiz(a) && Variante(a, c))
                    .Select(Raiz)
                    .Distinct()
                    .Take(2)
                    .ToList();
                if (candidatos.Count == 1) Juntar(candidatos[0], a);
            }
        }

        // 4. Monta cada médico: a forma mais completa dá o nome.
        return [.. chaves
            .GroupBy(Raiz)
            .Select(g =>
            {
                var membros = g.Select(k => porChave[k]).ToList();
                var principal = membros
                    .OrderByDescending(m => m.Chave.Split(' ').Length)
                    .ThenByDescending(m => m.Total)
                    .ThenBy(m => m.Chave, StringComparer.Ordinal)
                    .First();
                var cpf = Mais(membros.SelectMany(m => m.Cpfs));
                var conselho = Mais(membros.SelectMany(m => m.Conselhos))?.Split('|');
                return new MedicoConsolidado(
                    Mais(principal.Grafias)!,
                    principal.Chave,
                    [.. membros.Select(m => m.Chave).OrderBy(k => k, StringComparer.Ordinal)],
                    cpf,
                    conselho?[0],
                    conselho?[1],
                    conselho is { } c && c[2].Length == 2 ? c[2] : null,
                    membros.Sum(m => m.Total));
            })
            .OrderByDescending(m => m.Ocorrencias)
            .ThenBy(m => m.Chave, StringComparer.Ordinal)];
    }

    /// <summary>
    /// <paramref name="curto"/> é o mesmo nome que <paramref name="longo"/>, só abreviado ou com um
    /// erro de digitação: 3+ palavras, todas casando em ordem, primeiro e último nome por extenso.
    /// </summary>
    public static bool Variante(string curto, string longo)
    {
        var a = curto.Split(' ');
        var b = longo.Split(' ');
        if (a.Length < 3 || a.Length > b.Length) return false;
        if (a[0] != b[0] || a[^1].Length < 3) return false;
        var j = 0;
        string? ultimo = null;
        foreach (var x in a)
        {
            var achou = false;
            for (var k = j; k < b.Length; k++)
            {
                // O destino é a forma mais completa: "M" não completa "MARIA".
                if (b[k].Length < x.Length - 1 || !CasaSeguro(x, b[k])) continue;
                j = k + 1;
                ultimo = b[k];
                achou = true;
                break;
            }
            if (!achou) return false;
        }
        return a[^1] == ultimo;
    }

    /// <summary>
    /// Como <see cref="SemelhancaNome"/>, mas o erro de digitação só vale em palavra de 7+ letras:
    /// "FRANCICO" é "FRANCISCO", mas "MARTA" não é "MARIA" — e aqui ninguém confere antes de juntar.
    /// </summary>
    private static bool CasaSeguro(string x, string y) =>
        Math.Min(x.Length, y.Length) >= 7 ? SemelhancaNome.Casa(x, y)
        : x == y
          || (x.Length == 1 && y[0] == x[0])
          || (y.Length == 1 && x[0] == y[0])
          || (Math.Min(x.Length, y.Length) >= 3 && (x.StartsWith(y, StringComparison.Ordinal) || y.StartsWith(x, StringComparison.Ordinal)));

    private static bool SemIniciais(string chave) => chave.Split(' ').All(p => p.Length > 1);

    /// <summary>Só os 11 dígitos, se o CPF for válido (dígitos verificadores e não repetido).</summary>
    public static string? CpfValido(string? cpf)
    {
        var d = SemelhancaNome.Digitos(cpf);
        if (d.Length != 11 || d.Distinct().Count() == 1) return null;
        for (var t = 9; t < 11; t++)
        {
            var soma = 0;
            for (var i = 0; i < t; i++) soma += (d[i] - '0') * (t + 1 - i);
            var dv = soma * 10 % 11 % 10;
            if (d[t] - '0' != dv) return null;
        }
        return d;
    }

    /// <summary>MAIÚSCULAS e um espaço só entre as palavras.</summary>
    public static string Limpar(string nome) =>
        string.Join(' ', nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    // ---------------------------------------------------------------- apoio

    private static void Somar(Dictionary<string, int> d, string chave, int n) =>
        d[chave] = d.GetValueOrDefault(chave) + n;

    private static string? Mais(IEnumerable<KeyValuePair<string, int>> contagens) =>
        contagens
            .GroupBy(kv => kv.Key)
            .Select(g => (g.Key, Total: g.Sum(kv => kv.Value)))
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => x.Key)
            .FirstOrDefault();

    private sealed class Acumulado(string chave)
    {
        public string Chave { get; } = chave;
        public int Total { get; set; }
        public Dictionary<string, int> Grafias { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Cpfs { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Conselhos { get; } = new(StringComparer.Ordinal);
    }
}
