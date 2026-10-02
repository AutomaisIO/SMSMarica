using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Medicos;

public sealed record MedicoLocalDto(
    Guid Id,
    SistemaRegulacao Sistema,
    string Nome,
    string? Cpf,
    string? Conselho,
    string? NumeroConselho,
    string? UfConselho,
    IReadOnlyList<string> Grafias,
    OrigemMedicoLocal Origem,
    int Ocorrencias);

/// <param name="Motivo">Por que pareceu o mesmo ("CPF igual", "Nome parecido…").</param>
public sealed record MedicoLocalParecidoDto(MedicoLocalDto Medico, double Pontuacao, string Motivo);

/// <summary>Dois cadastros que podem ser o mesmo médico — a pessoa decide e junta.</summary>
public sealed record PossivelRepetidoDto(MedicoLocalDto A, MedicoLocalDto B, double Pontuacao);

public sealed record SalvarMedicoLocalRequest(
    SistemaRegulacao Sistema, string Nome, string? Cpf, string? Conselho, string? NumeroConselho, string? UfConselho);

public sealed record AtualizacaoMedicosLocaisDto(int Lidos, int Criados, int Atualizados, int Total);

public interface IRegulacaoMedicoLocalService
{
    /// <summary>Busca por palavras (em qualquer ordem, também nas outras grafias) ou pelo começo do CPF.</summary>
    Task<IReadOnlyList<MedicoLocalDto>> BuscarAsync(SistemaRegulacao sistema, string? termo, int limite, CancellationToken ct);

    /// <summary>"Já existe?" — antes de incluir.</summary>
    Task<IReadOnlyList<MedicoLocalParecidoDto>> ParecidosAsync(
        SistemaRegulacao sistema, string nome, string? cpf, CancellationToken ct);

    /// <summary>Inclui o médico — ou devolve o que já existe com o mesmo CPF ou o mesmo nome.</summary>
    Task<MedicoLocalDto> CriarAsync(SalvarMedicoLocalRequest req, CancellationToken ct);

    /// <summary>Corrige nome, CPF ou conselho. O nome antigo continua achando o médico.</summary>
    Task<MedicoLocalDto> AtualizarAsync(Guid id, SalvarMedicoLocalRequest req, CancellationToken ct);

    /// <summary>Junta <paramref name="origemId"/> em <paramref name="destinoId"/> e apaga o primeiro.</summary>
    Task<MedicoLocalDto> JuntarAsync(Guid destinoId, Guid origemId, CancellationToken ct);

    Task<IReadOnlyList<PossivelRepetidoDto>> PossiveisRepetidosAsync(SistemaRegulacao sistema, CancellationToken ct);

    /// <summary>Lê as fichas, os executantes e as solicitações e acrescenta o que faltar — sem duplicar.</summary>
    Task<AtualizacaoMedicosLocaisDto> AtualizarDasFichasAsync(SistemaRegulacao sistema, CancellationToken ct);
}

/// <summary>
/// O cadastro de médicos que é SÓ nosso (<see cref="RegulacaoMedicoLocal"/>) — para o SISREG, em
/// que o médico solicitante é texto digitado em cada pedido. Nada daqui vai para o SISREG: o que a
/// solicitação leva é o CPF e o nome, como texto, nos campos de sempre.
/// </summary>
public sealed class RegulacaoMedicoLocalService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual)
    : IRegulacaoMedicoLocalService
{
    /// <summary>A partir daqui o nome aparece como "pode ser o mesmo".</summary>
    private const double CorteNome = 0.75;

    public async Task<IReadOnlyList<MedicoLocalDto>> BuscarAsync(
        SistemaRegulacao sistema, string? termo, int limite, CancellationToken ct)
    {
        limite = Math.Clamp(limite, 1, 200);
        var digitos = SemelhancaNome.Digitos(termo);
        var palavras = SemelhancaNome.Palavras(termo);

        List<RegulacaoMedicoLocal> achados;
        if (palavras.Count == 0 && digitos.Length >= 3)
        {
            achados = await db.RegulacaoMedicosLocais.AsNoTracking()
                .Where(m => m.Sistema == sistema && m.Cpf != null && m.Cpf.StartsWith(digitos))
                .OrderByDescending(m => m.Ocorrencias).ThenBy(m => m.Nome)
                .Take(limite).ToListAsync(ct);
        }
        else if (palavras.Count == 0)
        {
            achados = await db.RegulacaoMedicosLocais.AsNoTracking()
                .Where(m => m.Sistema == sistema)
                .OrderByDescending(m => m.Ocorrencias).ThenBy(m => m.Nome)
                .Take(limite).ToListAsync(ct);
        }
        else
        {
            // Cada palavra digitada tem de começar uma palavra do nome ou de uma das grafias — a
            // mesma busca do campo de médico do SER ("andrade" acha o "A.", e vice-versa não).
            // As palavras são só letras A-Z (Palavras tira o resto), então entram cruas na regex.
            var padroes = palavras.Select(p => $"(^|[ \"]){p}").ToArray();
            achados = await db.RegulacaoMedicosLocais
                .FromSql($"""
                    SELECT * FROM smsmarica.regulacao_medico_local
                    WHERE sistema = {(int)sistema}
                      AND (nome_normalizado || ' ' || coalesce(grafias_json::text, '')) ~ ALL ({padroes})
                    """)
                .AsNoTracking()
                .OrderByDescending(m => m.Ocorrencias).ThenBy(m => m.Nome)
                .Take(limite).ToListAsync(ct);
        }

        return [.. achados.Select(Mapear)];
    }

    public async Task<IReadOnlyList<MedicoLocalParecidoDto>> ParecidosAsync(
        SistemaRegulacao sistema, string nome, string? cpf, CancellationToken ct)
    {
        var cpfDigitos = SemelhancaNome.Digitos(cpf);
        if (SemelhancaNome.Palavras(nome).Count == 0 && cpfDigitos.Length != 11) return [];

        var todos = await db.RegulacaoMedicosLocais.AsNoTracking().Where(m => m.Sistema == sistema).ToListAsync(ct);
        var achados = new List<MedicoLocalParecidoDto>();
        foreach (var m in todos)
        {
            if (cpfDigitos.Length == 11 && m.Cpf == cpfDigitos)
            {
                achados.Add(new(Mapear(m), 1.0, "CPF igual"));
                continue;
            }
            var (p, grafia) = MelhorPontuacao(nome, m);
            if (p < CorteNome) continue;
            var motivo = ConsolidacaoMedicos.Chave(nome) == ConsolidacaoMedicos.Chave(grafia)
                ? "Mesmo nome"
                : "Nome parecido (abreviado ou com sobrenome a mais)";
            achados.Add(new(Mapear(m), p, motivo));
        }

        return [.. achados
            .OrderByDescending(a => a.Pontuacao)
            .ThenByDescending(a => a.Medico.Ocorrencias)
            .Take(15)];
    }

    public async Task<MedicoLocalDto> CriarAsync(SalvarMedicoLocalRequest req, CancellationToken ct)
    {
        var (nome, chave, cpf, conselho, numero, uf) = Validar(req);

        // Sem duplicação: o mesmo CPF, ou o mesmo nome (também nas outras grafias), devolve o que
        // já existe — dois cliques, ou duas unidades incluindo o mesmo médico, não criam dois.
        var existente = await AcharAsync(req.Sistema, chave, cpf, null, ct);
        if (existente is not null)
        {
            // Achou pelo nome e quem incluiu trouxe o CPF (ou o conselho) que faltava: completa.
            var completou = false;
            if (existente.Cpf is null && cpf is not null)
            {
                existente.Cpf = cpf;
                completou = true;
            }
            if (existente.NumeroConselho is null && numero is not null)
            {
                existente.Conselho = conselho;
                existente.NumeroConselho = numero;
                existente.UfConselho = uf;
                completou = true;
            }
            if (completou)
            {
                existente.AtualizadoEm = DateTime.UtcNow;
                existente.AtualizadoPor = usuarioAtual.UsuarioId;
                await db.SaveChangesAsync(ct);
            }
            return Mapear(existente);
        }

        var novo = new RegulacaoMedicoLocal
        {
            Id = Guid.CreateVersion7(),
            Sistema = req.Sistema,
            Nome = nome,
            NomeNormalizado = chave,
            Cpf = cpf,
            Conselho = conselho,
            NumeroConselho = numero,
            UfConselho = uf,
            Origem = OrigemMedicoLocal.Plataforma,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.RegulacaoMedicosLocais.Add(novo);
        await db.SaveChangesAsync(ct);
        return Mapear(novo);
    }

    public async Task<MedicoLocalDto> AtualizarAsync(Guid id, SalvarMedicoLocalRequest req, CancellationToken ct)
    {
        var m = await db.RegulacaoMedicosLocais.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NaoEncontradoException("Médico", id);
        var (nome, chave, cpf, conselho, numero, uf) = Validar(req with { Sistema = m.Sistema });

        var outro = await AcharAsync(m.Sistema, chave, cpf, m.Id, ct);
        if (outro is not null)
        {
            throw new ConflitoException(
                "regulacao.medico_local_repetido",
                $"Já existe o cadastro \"{outro.Nome}\" com este {(cpf is not null && outro.Cpf == cpf ? "CPF" : "nome")}. Use \"Juntar\".");
        }

        if (chave != m.NomeNormalizado)
        {
            // O nome antigo vira grafia: quem procurar do jeito de antes continua achando.
            var grafias = Grafias(m).Append(m.NomeNormalizado).Where(g => g != chave).Distinct().ToList();
            m.GrafiasJson = Serializar(grafias);
            m.NomeNormalizado = chave;
        }
        m.Nome = nome;
        m.Cpf = cpf;
        m.Conselho = conselho;
        m.NumeroConselho = numero;
        m.UfConselho = uf;
        m.AtualizadoEm = DateTime.UtcNow;
        m.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        return Mapear(m);
    }

    public async Task<MedicoLocalDto> JuntarAsync(Guid destinoId, Guid origemId, CancellationToken ct)
    {
        if (destinoId == origemId) throw new ValidacaoException("origemId", "Escolha dois cadastros diferentes.");
        var destino = await db.RegulacaoMedicosLocais.FirstOrDefaultAsync(x => x.Id == destinoId, ct)
                      ?? throw new NaoEncontradoException("Médico", destinoId);
        var origem = await db.RegulacaoMedicosLocais.FirstOrDefaultAsync(x => x.Id == origemId, ct)
                     ?? throw new NaoEncontradoException("Médico", origemId);
        if (destino.Sistema != origem.Sistema)
        {
            throw new ValidacaoException("origemId", "Os dois cadastros têm de ser do mesmo sistema.");
        }
        if (destino.Cpf is not null && origem.Cpf is not null && destino.Cpf != origem.Cpf)
        {
            throw new ConflitoException(
                "regulacao.medico_local_cpfs_diferentes",
                "Os dois têm CPFs diferentes — são duas pessoas. Corrija o CPF errado antes de juntar.");
        }

        destino.GrafiasJson = Serializar(Grafias(destino)
            .Concat(Grafias(origem))
            .Append(origem.NomeNormalizado)
            .Where(g => g != destino.NomeNormalizado)
            .Distinct());
        destino.Ocorrencias += origem.Ocorrencias;
        destino.Cpf ??= origem.Cpf;
        if (destino.NumeroConselho is null && origem.NumeroConselho is not null)
        {
            destino.Conselho = origem.Conselho;
            destino.NumeroConselho = origem.NumeroConselho;
            destino.UfConselho = origem.UfConselho;
        }
        destino.AtualizadoEm = DateTime.UtcNow;
        destino.AtualizadoPor = usuarioAtual.UsuarioId;

        // O CPF muda de dono no mesmo SaveChanges: apaga antes de gravar para o índice único não
        // ver os dois ao mesmo tempo.
        db.RegulacaoMedicosLocais.Remove(origem);
        if (destino.Cpf == origem.Cpf && origem.Cpf is not null)
        {
            var cpf = destino.Cpf;
            destino.Cpf = null;
            await db.SaveChangesAsync(ct);
            destino.Cpf = cpf;
        }
        await db.SaveChangesAsync(ct);
        return Mapear(destino);
    }

    public async Task<IReadOnlyList<PossivelRepetidoDto>> PossiveisRepetidosAsync(SistemaRegulacao sistema, CancellationToken ct)
    {
        var todos = await db.RegulacaoMedicosLocais.AsNoTracking().Where(m => m.Sistema == sistema).ToListAsync(ct);
        var pares = new List<PossivelRepetidoDto>();
        foreach (var bloco in todos.GroupBy(m => m.NomeNormalizado.Split(' ')[0]))
        {
            var lista = bloco.ToList();
            for (var i = 0; i < lista.Count; i++)
            {
                for (var j = i + 1; j < lista.Count; j++)
                {
                    var a = lista[i];
                    var b = lista[j];
                    if (a.Cpf is not null && b.Cpf is not null && a.Cpf != b.Cpf) continue;
                    var p = SemelhancaNome.Pontuacao(a.NomeNormalizado, b.NomeNormalizado);
                    if (p < 1.0) continue;
                    var (maior, menor) = a.Ocorrencias >= b.Ocorrencias ? (a, b) : (b, a);
                    pares.Add(new(Mapear(maior), Mapear(menor), p));
                }
            }
        }

        return [.. pares.OrderByDescending(p => p.A.Ocorrencias + p.B.Ocorrencias).Take(300)];
    }

    public async Task<AtualizacaoMedicosLocaisDto> AtualizarDasFichasAsync(SistemaRegulacao sistema, CancellationToken ct)
    {
        if (sistema != SistemaRegulacao.Sisreg)
        {
            throw new ValidacaoException("sistema", "Só o SISREG tem cadastro de médicos nosso: os outros sistemas têm lista própria.");
        }

        var ocorrencias = await LerFontesSisregAsync(ct);
        var consolidados = ConsolidacaoMedicos.Consolidar(ocorrencias.Select(o => o.Ocorrencia));
        var soExecutante = ocorrencias
            .Where(o => o.Executante)
            .Select(o => ConsolidacaoMedicos.Chave(o.Ocorrencia.Nome))
            .ToHashSet(StringComparer.Ordinal);
        var deFicha = ocorrencias
            .Where(o => !o.Executante)
            .Select(o => ConsolidacaoMedicos.Chave(o.Ocorrencia.Nome))
            .ToHashSet(StringComparer.Ordinal);

        var existentes = await db.RegulacaoMedicosLocais.Where(m => m.Sistema == sistema).ToListAsync(ct);
        var porChave = new Dictionary<string, RegulacaoMedicoLocal>(StringComparer.Ordinal);
        var porCpf = new Dictionary<string, RegulacaoMedicoLocal>(StringComparer.Ordinal);
        foreach (var m in existentes) Indexar(m);

        var contagem = new Dictionary<Guid, int>();
        var agora = DateTime.UtcNow;
        int criados = 0, atualizados = 0;

        foreach (var c in consolidados)
        {
            var m = (c.Cpf is not null ? porCpf.GetValueOrDefault(c.Cpf) : null)
                    ?? c.Chaves.Select(k => porChave.GetValueOrDefault(k)).FirstOrDefault(x => x is not null);

            if (m is null)
            {
                m = new RegulacaoMedicoLocal
                {
                    Id = Guid.CreateVersion7(),
                    Sistema = sistema,
                    Nome = c.Nome,
                    NomeNormalizado = c.Chave,
                    Cpf = c.Cpf,
                    Conselho = c.Conselho,
                    NumeroConselho = c.NumeroConselho,
                    UfConselho = c.UfConselho,
                    GrafiasJson = Serializar(c.Chaves.Where(k => k != c.Chave)),
                    Origem = c.Chaves.Any(deFicha.Contains) || !c.Chaves.Any(soExecutante.Contains)
                        ? OrigemMedicoLocal.Fichas
                        : OrigemMedicoLocal.Executantes,
                    CriadoEm = agora,
                };
                db.RegulacaoMedicosLocais.Add(m);
                Indexar(m);
                criados++;
            }
            else
            {
                var mudou = false;
                // Grafia nova entra — se não for o nome de OUTRO cadastro (esses a pessoa junta).
                var novas = c.Chaves
                    .Where(k => k != m.NomeNormalizado && !porChave.ContainsKey(k))
                    .ToList();
                if (novas.Count > 0)
                {
                    m.GrafiasJson = Serializar(Grafias(m).Concat(novas).Distinct());
                    foreach (var k in novas) porChave[k] = m;
                    mudou = true;
                }
                // Completa o que faltava; o que alguém já preencheu, a ficha não sobrescreve.
                if (m.Cpf is null && c.Cpf is not null && !porCpf.ContainsKey(c.Cpf))
                {
                    m.Cpf = c.Cpf;
                    porCpf[c.Cpf] = m;
                    mudou = true;
                }
                if (m.NumeroConselho is null && c.NumeroConselho is not null)
                {
                    m.Conselho = c.Conselho;
                    m.NumeroConselho = c.NumeroConselho;
                    m.UfConselho = c.UfConselho;
                    mudou = true;
                }
                if (mudou)
                {
                    m.AtualizadoEm = agora;
                    atualizados++;
                }
            }

            contagem[m.Id] = contagem.GetValueOrDefault(m.Id) + c.Ocorrencias;
        }

        // A contagem é refeita do zero a cada leitura (as fichas são a fonte); quem não apareceu
        // nelas (incluído pela plataforma e ainda não usado) mantém a sua.
        foreach (var m in db.RegulacaoMedicosLocais.Local.Where(x => x.Sistema == sistema))
        {
            if (contagem.TryGetValue(m.Id, out var n) && m.Ocorrencias != n) m.Ocorrencias = n;
        }

        await db.SaveChangesAsync(ct);
        var total = await db.RegulacaoMedicosLocais.CountAsync(m => m.Sistema == sistema, ct);
        return new(ocorrencias.Count, criados, atualizados, total);

        void Indexar(RegulacaoMedicoLocal m)
        {
            porChave.TryAdd(m.NomeNormalizado, m);
            foreach (var g in Grafias(m)) porChave.TryAdd(g, m);
            if (m.Cpf is not null) porCpf.TryAdd(m.Cpf, m);
        }
    }

    // ---------------------------------------------------------------- apoio

    private sealed record Fonte(OcorrenciaMedico Ocorrencia, bool Executante);

    /// <summary>
    /// O SISREG não tem cadastro de médico solicitante para ler: o que existe é o nome (e às vezes o
    /// CPF e o conselho) em cada ficha importada, os executantes de cada unidade e o que foi
    /// digitado nas nossas solicitações com destino SISREG. Nenhuma requisição ao SISREG.
    /// </summary>
    private async Task<List<Fonte>> LerFontesSisregAsync(CancellationToken ct)
    {
        var fichas = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.SolicitanteNome != "")
            .GroupBy(s => new
            {
                Nome = s.SolicitanteNome.Trim().ToUpper(),
                s.SolicitanteCpf,
                s.SolicitanteConselho,
                s.SolicitanteNumConselho,
                s.SolicitanteUfConselho,
            })
            .Select(g => new
            {
                g.Key.Nome,
                g.Key.SolicitanteCpf,
                g.Key.SolicitanteConselho,
                g.Key.SolicitanteNumConselho,
                g.Key.SolicitanteUfConselho,
                Quantidade = g.Count(),
            })
            .ToListAsync(ct);

        var executantes = await db.SisregProfissionaisUnidade.AsNoTracking()
            .Select(p => new { p.Nome, p.Cpf })
            .Distinct()
            .ToListAsync(ct);

        var digitados = await db.Database
            .SqlQuery<MedicoDigitado>($"""
                SELECT upper(trim(formulario_json -> 'canonico' ->> 'profissional_solicitante_nome')) AS "Nome",
                       formulario_json -> 'canonico' ->> 'profissional_solicitante_cpf' AS "Cpf",
                       count(*)::int AS "Quantidade"
                FROM smsmarica.regulacao_solicitacao
                WHERE excluido_em IS NULL
                  AND coalesce(formulario_json -> 'canonico' ->> 'profissional_solicitante_nome', '') <> ''
                GROUP BY 1, 2
                """)
            .ToListAsync(ct);

        return
        [
            .. fichas.Select(f => new Fonte(
                new(f.Nome, f.SolicitanteCpf, f.SolicitanteConselho, f.SolicitanteNumConselho, f.SolicitanteUfConselho, f.Quantidade),
                false)),
            .. executantes.Select(e => new Fonte(new(e.Nome, e.Cpf, null, null, null, 0), true)),
            .. digitados.Select(d => new Fonte(new(d.Nome, d.Cpf, null, null, null, d.Quantidade), false)),
        ];
    }

    private sealed class MedicoDigitado
    {
        public string Nome { get; set; } = string.Empty;
        public string? Cpf { get; set; }
        public int Quantidade { get; set; }
    }

    /// <summary>O registro com este CPF ou com este nome (também como outra grafia), fora <paramref name="exceto"/>.</summary>
    private async Task<RegulacaoMedicoLocal?> AcharAsync(
        SistemaRegulacao sistema, string chave, string? cpf, Guid? exceto, CancellationToken ct)
    {
        if (cpf is not null)
        {
            var porCpf = await db.RegulacaoMedicosLocais
                .FirstOrDefaultAsync(m => m.Sistema == sistema && m.Cpf == cpf && m.Id != exceto, ct);
            if (porCpf is not null) return porCpf;
        }
        var grafiaJson = JsonSerializer.Serialize(new[] { chave });
        return await db.RegulacaoMedicosLocais
            .FromSql($"""
                SELECT * FROM smsmarica.regulacao_medico_local
                WHERE sistema = {(int)sistema}
                  AND (nome_normalizado = {chave} OR coalesce(grafias_json, '[]'::jsonb) @> {grafiaJson}::jsonb)
                """)
            .Where(m => m.Id != exceto)
            .FirstOrDefaultAsync(ct);
    }

    private static (string Nome, string Chave, string? Cpf, string? Conselho, string? Numero, string? Uf) Validar(
        SalvarMedicoLocalRequest req)
    {
        if (req.Sistema != SistemaRegulacao.Sisreg)
        {
            throw new ValidacaoException("sistema", "Só o SISREG tem cadastro de médicos nosso: nos outros, peça o cadastro ao sistema.");
        }
        var nome = ConsolidacaoMedicos.Limpar(req.Nome ?? string.Empty);
        if (!ConsolidacaoMedicos.NomeAproveitavel(nome))
        {
            throw new ValidacaoException("nome", "Digite o nome completo do médico (nome e sobrenome).");
        }
        if (nome.Length > 300) throw new ValidacaoException("nome", "O nome cabe em até 300 caracteres.");

        string? cpf = null;
        if (!string.IsNullOrWhiteSpace(req.Cpf))
        {
            cpf = ConsolidacaoMedicos.CpfValido(req.Cpf)
                  ?? throw new ValidacaoException("cpf", "CPF inválido — confira os números (ou deixe em branco).");
        }

        var numero = string.IsNullOrWhiteSpace(req.NumeroConselho) ? null : req.NumeroConselho.Trim();
        if (numero is { Length: > 30 }) throw new ValidacaoException("numeroConselho", "O número cabe em até 30 caracteres.");
        var conselho = numero is null ? null : (string.IsNullOrWhiteSpace(req.Conselho) ? "CRM" : req.Conselho.Trim().ToUpperInvariant());
        if (conselho is { Length: > 20 }) throw new ValidacaoException("conselho", "O conselho cabe em até 20 caracteres.");
        var uf = numero is null || string.IsNullOrWhiteSpace(req.UfConselho) ? null : req.UfConselho.Trim().ToUpperInvariant();
        if (uf is not null && uf.Length != 2) throw new ValidacaoException("ufConselho", "UF do conselho com 2 letras (RJ).");

        return (nome, ConsolidacaoMedicos.Chave(nome), cpf, conselho, numero, uf);
    }

    private static (double Pontuacao, string Grafia) MelhorPontuacao(string nome, RegulacaoMedicoLocal m) =>
        Grafias(m)
            .Prepend(m.NomeNormalizado)
            .Select(g => (SemelhancaNome.Pontuacao(nome, g), g))
            .MaxBy(x => x.Item1);

    private static IReadOnlyList<string> Grafias(RegulacaoMedicoLocal m) =>
        string.IsNullOrEmpty(m.GrafiasJson) ? [] : JsonSerializer.Deserialize<List<string>>(m.GrafiasJson) ?? [];

    private static string? Serializar(IEnumerable<string> grafias)
    {
        var lista = grafias.OrderBy(g => g, StringComparer.Ordinal).ToList();
        return lista.Count == 0 ? null : JsonSerializer.Serialize(lista);
    }

    private static MedicoLocalDto Mapear(RegulacaoMedicoLocal m) =>
        new(m.Id, m.Sistema, m.Nome, m.Cpf, m.Conselho, m.NumeroConselho, m.UfConselho, Grafias(m), m.Origem, m.Ocorrencias);
}
