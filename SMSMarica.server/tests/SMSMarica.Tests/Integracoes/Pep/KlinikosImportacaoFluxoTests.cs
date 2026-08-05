using System.Text.RegularExpressions;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Integracoes.Pep.Divergencias;
using SMSMarica.Core.Integracoes.Pep.Estrategias;
using SMSMarica.Core.Integracoes.Pep.Estrategias.Klinikos;
using SMSMarica.Core.Integracoes.Pep.Fhir;
using SMSMarica.Core.Integracoes.Pep.Progresso;
using SMSMarica.Core.Inteligencia.Fontes;
using SMSMarica.Data.Entities.Enums;
using Task = System.Threading.Tasks.Task;

namespace SMSMarica.Tests.Integracoes.Pep;

/// <summary>
/// O run inteiro do conector Klinikos contra uma ORIGEM fake e um HUB fake — as garantias que
/// o incidente de 04/08/2026 provou que precisam de teste, não de fé:
///
/// <list type="number">
/// <item>paciente e profissional amarrados pelo CPF — a mesma pessoa em duas bases converge
/// para UM recurso, com os identifiers das duas;</item>
/// <item>sem CPF entra MARCADO e nunca funde com ninguém;</item>
/// <item>escrita que falha segura o ponteiro — e o ciclo seguinte recupera tudo;</item>
/// <item>nascimento divergente no mesmo CPF congela e vira divergência, não sobrescrita.</item>
/// </list>
///
/// <para>O hub fake devolve <b>405 no <c>PUT ?identifier=</c> de Patient/Practitioner</b>,
/// exatamente como o hub real: se alguém religar identidade no atalho dos recursos clínicos,
/// esta suíte inteira quebra — que é o ponto.</para>
/// </summary>
public class KlinikosImportacaoFluxoTests
{
    private const string Slug = "upa24h-marica-sqlserver";
    private const string SysCpf = "https://fhir.saude.gov.br/sid/cpf";

    // ---------------------------------------------------------------- origem fake

    /// <summary>
    /// Origem Klinikos de mentira: entende exatamente as consultas que a estratégia emite
    /// (keyset por rv, <c>IN</c> por código, flag de atendimento) sobre tabelas em memória.
    /// </summary>
    private sealed class FonteFake : IFonteDados
    {
        public List<Dictionary<string, object?>> Unidades { get; } = [];
        public List<Dictionary<string, object?>> Profissionais { get; } = [];
        public List<Dictionary<string, object?>> Pacientes { get; } = [];
        public List<Dictionary<string, object?>> Boletins { get; } = [];
        public List<Dictionary<string, object?>> Evolucoes { get; } = [];
        public List<Dictionary<string, object?>> Sinais { get; } = [];

        public bool Conectada { get; set; } = true;

        /// <summary>Teto do MIN_ACTIVE_ROWVERSION simulado (default: sem transação em voo).</summary>
        public long CapRowversion { get; set; } = long.MaxValue - 1;

        public Task<bool> TestarConexaoAsync(CancellationToken ct = default) => Task.FromResult(Conectada);

        public Task<ResultadoConsulta> ExecutarAsync(
            string sql, CancellationToken ct = default, int? maxLinhasOverride = null)
        {
            // Cap do rowversion (corrida de transação em voo): o fake não tem transações,
            // devolve teto altíssimo — os testes de cap forçam valores menores por override.
            if (sql.Contains("MIN_ACTIVE_ROWVERSION", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Tabela([new Dictionary<string, object?> { ["cap"] = CapRowversion }]));
            }

            // Flag de atendimento: DISTINCT SPA_CODIGO das evoluções não-ESTORNO.
            if (sql.Contains("DISTINCT SPA_CODIGO", StringComparison.OrdinalIgnoreCase))
            {
                var lista = FiltroIn(sql).Valores;
                var spas = Evolucoes
                    .Where(e => e["SPA_CODIGO"] is string spa && lista.Contains(spa)
                        && e["Tipo"] is string t && !t.StartsWith("ESTORNO", StringComparison.OrdinalIgnoreCase))
                    .Select(e => (string)e["SPA_CODIGO"]!)
                    .Distinct()
                    .ToList();
                return Task.FromResult(Tabela(spas.Select(s =>
                    new Dictionary<string, object?> { ["SPA_CODIGO"] = s }).ToList()));
            }

            var tabela = TabelaDe(sql);
            var linhas = tabela;

            if (FiltroIn(sql) is ({ } coluna, { Count: > 0 } codigos))
            {
                // A coluna do filtro é a que está ESCRITA no WHERE — adivinhá-la pela tabela
                // fazia `Pronto_Atendimento WHERE pac_codigo IN (...)` filtrar por spa_codigo
                // e devolver vazio, escondendo do teste um caminho que funciona em produção.
                linhas = [.. tabela.Where(r => r.GetValueOrDefault(coluna) is string c && codigos.Contains(c))];
            }
            else if (Regex.Match(sql, @">\s*(\d+)") is { Success: true } m)
            {
                var desde = long.Parse(m.Groups[1].Value);
                linhas = [.. tabela
                    .Where(r => Convert.ToInt64(r["rv"]!) > desde)
                    .OrderBy(r => Convert.ToInt64(r["rv"]!))];
                if (Regex.Match(sql, @"TOP\s+(\d+)") is { Success: true } t)
                    linhas = [.. linhas.Take(int.Parse(t.Groups[1].Value))];
            }

            return Task.FromResult(Tabela(linhas));
        }

        private List<Dictionary<string, object?>> TabelaDe(string sql) => sql switch
        {
            _ when sql.Contains("FROM unidade", StringComparison.OrdinalIgnoreCase) => Unidades,
            _ when sql.Contains("FROM profissional", StringComparison.OrdinalIgnoreCase) => Profissionais,
            _ when sql.Contains("FROM paciente", StringComparison.OrdinalIgnoreCase) => Pacientes,
            _ when sql.Contains("FROM Pronto_Atendimento", StringComparison.OrdinalIgnoreCase) => Boletins,
            _ when sql.Contains("FROM UPA_Evolucao", StringComparison.OrdinalIgnoreCase) => Evolucoes,
            _ when sql.Contains("FROM UPA_SinaisVitais", StringComparison.OrdinalIgnoreCase) => Sinais,
            _ => throw new InvalidOperationException($"SQL não previsto pelo fake: {sql[..Math.Min(80, sql.Length)]}"),
        };

        /// <summary>Coluna e valores de um <c>&lt;coluna&gt; IN ('a','b')</c>, como o conector escreve.</summary>
        private static (string? Coluna, HashSet<string> Valores) FiltroIn(string sql)
        {
            var m = Regex.Match(sql, @"(\w+)\s+IN \(([^)]+)\)");
            return !m.Success
                ? (null, [])
                : (m.Groups[1].Value, [.. m.Groups[2].Value.Split(',').Select(v => v.Trim().Trim('\''))]);
        }

        private static ResultadoConsulta Tabela(List<Dictionary<string, object?>> linhas)
        {
            if (linhas.Count == 0) return new ResultadoConsulta(true, [], []);
            var colunas = linhas[0].Keys.ToList();
            var valores = linhas
                .Select(l => (IReadOnlyList<object?>)[.. colunas.Select(c => l.GetValueOrDefault(c))])
                .ToList();
            return new ResultadoConsulta(true, colunas, valores);
        }
    }

    // ---------------------------------------------------------------- hub fake

    /// <summary>
    /// Hub FHIR de mentira com a MESMA regra do real que derrubou a primeira versão do
    /// conector: <c>PUT ?identifier=</c> de Patient/Practitioner responde 405 — identidade só
    /// entra pelo caminho canônico (busca → merge → update/create).
    /// </summary>
    private sealed class HubFake : IHubFhirEscritor
    {
        public List<Resource> Recursos { get; } = [];

        /// <summary>Injeção de falha: escreve-se falha quando o predicado casa.</summary>
        public Func<Resource, bool>? FalharSe { get; set; }

        public Task<Resource> CriarAsync(Resource recurso, CancellationToken ct = default)
        {
            Falha(recurso);
            var copia = (Resource)recurso.DeepCopy();
            copia.Id = Guid.NewGuid().ToString();
            copia.Meta ??= new Meta();
            copia.Meta.VersionId = "1";
            Recursos.Add(copia);
            return Task.FromResult((Resource)copia.DeepCopy());
        }

        public Task<Resource> AtualizarAsync(string tipo, string id, Resource recurso, CancellationToken ct = default)
        {
            Falha(recurso);
            var i = Recursos.FindIndex(r => r.TypeName == tipo && r.Id == id);
            if (i < 0) throw new HubFhirHttpException(404, "não achado", $"PUT {tipo}/{id}");
            var copia = (Resource)recurso.DeepCopy();
            copia.Id = id;
            copia.Meta ??= new Meta();
            copia.Meta.VersionId = (int.Parse(Recursos[i].Meta?.VersionId ?? "1") + 1).ToString();
            Recursos[i] = copia;
            return Task.FromResult((Resource)copia.DeepCopy());
        }

        public Task<Resource> UpsertPorIdentifierAsync(Resource recurso, string system, string value, CancellationToken ct = default)
        {
            // A regra do hub real (e a razão do incidente): identidade NÃO tem update condicional.
            if (recurso is Patient or Practitioner)
                throw new HubFhirHttpException(405, string.Empty, $"PUT fhir/{recurso.TypeName}?identifier=");

            Falha(recurso);
            var achado = Recursos.FirstOrDefault(r =>
                r.TypeName == recurso.TypeName && TemIdentifier(r, system, value));
            if (achado is null) return CriarAsync(recurso, ct);

            var copia = (Resource)recurso.DeepCopy();
            copia.Id = achado.Id;
            Recursos[Recursos.IndexOf(achado)] = copia;
            return Task.FromResult((Resource)copia.DeepCopy());
        }

        public Task<Bundle> BuscarPorIdentifierAsync(string tipo, string system, string value, CancellationToken ct = default)
        {
            var bundle = new Bundle { Type = Bundle.BundleType.Searchset };
            foreach (var r in Recursos.Where(r => r.TypeName == tipo && TemIdentifier(r, system, value)))
                bundle.Entry.Add(new Bundle.EntryComponent { Resource = (Resource)r.DeepCopy() });
            return Task.FromResult(bundle);
        }

        public Task ExcluirAsync(string tipo, string id, CancellationToken ct = default)
        {
            Recursos.RemoveAll(r => r.TypeName == tipo && r.Id == id);
            return Task.CompletedTask;
        }

        public Task<Bundle> BuscarPorPacienteAsync(string tipo, string pacienteId, CancellationToken ct = default) =>
            Task.FromResult(new Bundle { Type = Bundle.BundleType.Searchset });

        public Task<Bundle> ListarAsync(string tipo, CancellationToken ct = default) =>
            Task.FromResult(new Bundle { Type = Bundle.BundleType.Searchset });

        public Task<string> ObterEstatisticasAsync(string source, CancellationToken ct = default) =>
            Task.FromResult("{}");

        private void Falha(Resource r)
        {
            if (FalharSe?.Invoke(r) == true)
                throw new HubFhirHttpException(500, "falha injetada pelo teste", r.TypeName);
        }

        private static bool TemIdentifier(Resource r, string system, string value) =>
            (r as IIdentifiable<List<Identifier>>)?.Identifier?
                .Any(i => i.System == system && i.Value == value) == true;

        public List<T> Do<T>() where T : Resource => [.. Recursos.OfType<T>()];
    }

    private sealed class DivergenciasFake : IRegistradorDivergenciasPep
    {
        public List<DivergenciaDetectada> Registradas { get; } = [];
        public void Registrar(DivergenciaDetectada divergencia) => Registradas.Add(divergencia);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // ---------------------------------------------------------------- cenário

    private static FonteFake OrigemPadrao()
    {
        var f = new FonteFake();
        f.Unidades.Add(new()
        {
            ["unid_codigo"] = "0006", ["unid_descricao"] = "UPA MARICA",
            ["Unid_nome_fantasia"] = "UPA MARICA", ["unid_sigla"] = "UPAMARICA",
            ["unid_codigoCNES"] = "7164440", ["unid_telefone"] = null, ["unid_email"] = null,
        });
        f.Profissionais.Add(Prof("0001", "DR HOUSE", "39053344705"));
        f.Profissionais.Add(Prof("0002", "SEM DOCUMENTO", null)); // fica fora — regra canônica
        f.Pacientes.Add(Pac("P1", "ANA COM CPF", "52998224725", "1980-05-10", 100));
        f.Pacientes.Add(Pac("P2", "BENTO SEM CPF", null, "2024-01-01", 200));
        f.Pacientes.Add(Pac("P3", "CARLA COM CPF", "11144477735", "1990-12-30", 300));
        f.Boletins.Add(Bol("B1", "P1", 1000));
        f.Boletins.Add(Bol("B2", "P2", 1100)); // evasão: nenhum INÍCIO
        f.Boletins.Add(Bol("B3", "P3", 1200));
        f.Evolucoes.Add(Evo(1, "B1", "INÍCIO DO ATENDIMENTO MÉDICO", "Início do Atendimento", "J06.9", 2000));
        f.Evolucoes.Add(Evo(2, "B1", "RECEITA", "dipirona 500mg 6/6h", null, 2100));
        f.Evolucoes.Add(Evo(3, "B3", "EVOLUÇÃO MÉDICA", "paciente estável, mantém conduta", null, 2200));
        f.Evolucoes.Add(Evo(4, "B3", "ESTORNO", "lançamento anulado", null, 2300));
        f.Evolucoes.Add(Evo(5, "B3", "INÍCIO DO ATENDIMENTO MÉDICO", "Início do Atendimento", "A90", 2400));
        f.Sinais.Add(new()
        {
            ["sv_codigo"] = 77L, ["spa_codigo"] = "B1", ["data"] = "2026-08-01T10:20:00",
            ["prof_codigo"] = "0001", ["pressaoarterial"] = "120/80", ["pulso"] = "88",
            ["temperatura"] = null, ["frequenciarespiratoria"] = null, ["hgt"] = null,
            ["saturacaoO2"] = null, ["peso"] = null, ["rv"] = 3000L,
        });
        return f;
    }

    private static Dictionary<string, object?> Prof(string cod, string nome, string? cpf) => new()
    {
        ["PROF_CODIGO"] = cod, ["PROF_NOME"] = nome, ["PROF_CPF"] = cpf, ["PROF_CNS"] = null,
        ["PROF_NUMCONSELHO"] = "52123", ["CBO_CODIGO"] = "225125", ["PROF_ATIVO"] = "S",
    };

    private static Dictionary<string, object?> Pac(string cod, string nome, string? cpf, string nasc, long rv) => new()
    {
        ["pac_codigo"] = cod, ["pac_nome"] = nome, ["pac_cpf"] = cpf, ["pac_cartao_nsaude"] = null,
        ["pac_nascimento"] = nasc + "T00:00:00", ["pac_sexo"] = "F", ["pac_mae"] = "MAE " + nome,
        ["pac_pai"] = null, ["pac_telefone"] = "0000000000", ["pac_celular"] = "21993094621",
        ["pac_email"] = null, ["pac_dtobito"] = null, ["pac_responsavel"] = null,
        ["pac_telefone_responsavel"] = null, ["pac_raca"] = null, ["rv"] = rv,
    };

    private static Dictionary<string, object?> Bol(string cod, string pac, long rv) => new()
    {
        ["spa_codigo"] = cod, ["pac_codigo"] = pac, ["unid_codigo"] = "0006",
        ["spa_chegada"] = "2026-08-01T09:00:00", ["spa_dt_boletim"] = "2026-08-01T09:05:00",
        ["spa_nomesocial"] = null, ["spa_cartao_nsaude"] = null, ["spa_forma_chegada"] = "1",
        ["risaco_codigo"] = "3", ["rv"] = rv,
    };

    private static Dictionary<string, object?> Evo(long cod, string spa, string tipo, string desc, string? cid, long rv) => new()
    {
        ["upaevo_codigo"] = cod, ["SPA_CODIGO"] = spa, ["Tipo"] = tipo,
        ["upaevo_datahora"] = "2026-08-01T10:00:00", ["upaevo_descricao"] = desc,
        ["prof_codigo"] = "0001", ["cid_codigo_primario"] = cid, ["cid_codigo_secundario"] = null,
        ["rv"] = rv,
    };

    private static async Task<(ContextoImportacaoPep Ctx, ProgressoImportacao P, DivergenciasFake Div)> RodarAsync(
        FonteFake fonte, HubFake hub, MarcaDagua? marca = null,
        IReadOnlyDictionary<string, bool>? conhecidas = null)
    {
        var progresso = new ProgressoImportacao();
        var div = new DivergenciasFake();
        var ctx = new ContextoImportacaoPep
        {
            Consulta = fonte,
            Opcoes = new OpcoesImportacao(ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo, null, null, false),
            Marca = marca ?? new MarcaDagua(),
            Escritor = hub,
            Progresso = progresso,
            BaseSlug = Slug,
            Divergencias = div,
            DivergenciasConhecidas = conhecidas ?? new Dictionary<string, bool>(),
        };
        var estrategia = new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance);
        await estrategia.ImportarAsync(ctx, CancellationToken.None);
        return (ctx, progresso, div);
    }

    // ---------------------------------------------------------------- os testes

    [Fact]
    public async Task Carga_inicial__tudo_entra_e_os_ponteiros_avancam_ao_maximo()
    {
        var hub = new HubFake();
        var (ctx, p, _) = await RodarAsync(OrigemPadrao(), hub);

        // Identidade: 3 pacientes (1 marcado), 1 profissional (o sem CPF fica fora).
        Assert.Equal(3, hub.Do<Patient>().Count);
        Assert.Single(hub.Do<Practitioner>());
        var semCpf = Assert.Single(hub.Do<Patient>(), x => x.Meta?.Tag?.Any(t => t.Code == "identidade-incompleta") == true);
        Assert.Equal("BENTO SEM CPF", semCpf.Name[0].Text);

        // Clínico: 3 Encounters — B2 (evasão) entra com status próprio, não some.
        var encs = hub.Do<Encounter>();
        Assert.Equal(3, encs.Count);
        Assert.Equal(2, encs.Count(e => e.Status == Encounter.EncounterStatus.Finished));
        var evasao = Assert.Single(encs, e => e.Status == Encounter.EncounterStatus.Cancelled);
        Assert.Contains(evasao.Identifier, i => i.Value == $"{Slug}:B2");

        // Condition por boletim; RECEITA vira MedicationRequest; ESTORNO e INÍCIO não viram documento.
        Assert.Equal(2, hub.Do<Condition>().Count);        // B1 (J06.9) e B3 (A90)
        Assert.Single(hub.Do<MedicationRequest>());
        var doc = Assert.Single(hub.Do<DocumentReference>());
        Assert.Contains("mantém conduta", System.Text.Encoding.UTF8.GetString(doc.Content[0].Attachment.Data!));

        // Sinais: PA (painel) + pulso = 2 Observations.
        Assert.Equal(2, hub.Do<Observation>().Count);

        // Ponteiros no máximo de cada fase — nada falhou.
        Assert.Equal(0, p.FalhasTotal);
        Assert.Equal(300, ctx.Marca.Ponteiro("paciente"));
        Assert.Equal(1200, ctx.Marca.Ponteiro("atendimento"));
        Assert.Equal(2400, ctx.Marca.Ponteiro("evolucao"));
        Assert.Equal(3000, ctx.Marca.Ponteiro("sinais-vitais"));
    }

    /// <summary>A garantia número 1: mesma pessoa nas duas bases = UM recurso, amarrado pelo CPF.</summary>
    [Fact]
    public async Task Mesmo_CPF_vindo_do_Salux_e_do_Klinikos_converge_para_UM_paciente()
    {
        var hub = new HubFake();
        var doSalux = new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA COM CPF" }],
            BirthDate = "1980-05-10",
            Identifier =
            [
                new Identifier(SysCpf, "52998224725"),
                new Identifier("urn:salux:cd_paciente", "salux-hcml:777"),
            ],
        };
        await hub.CriarAsync(doSalux);

        var (_, _, _) = await RodarAsync(OrigemPadrao(), hub);

        var comCpf = hub.Do<Patient>().Where(x => x.Identifier.Any(i => i.System == SysCpf && i.Value == "52998224725")).ToList();
        var unico = Assert.Single(comCpf);

        // Os identifiers das DUAS bases acumulam no mesmo recurso — o rastro ADR-0009.
        Assert.Contains(unico.Identifier, i => i.System == "urn:salux:cd_paciente" && i.Value == "salux-hcml:777");
        Assert.Contains(unico.Identifier, i => i.System == "urn:klinikos:paciente" && i.Value == $"{Slug}:P1");
        Assert.Equal(3, hub.Do<Patient>().Count); // 1 fundido + P2 + P3 — nada duplicou
    }

    /// <summary>
    /// Sem CPF NÃO existe fusão — nem com nome e nascimento idênticos. Homônimo com a mesma
    /// data existe; fundir dois pacientes é o pior desfecho possível num prontuário.
    /// </summary>
    [Fact]
    public async Task Sem_CPF_nunca_funde__nem_com_nome_e_nascimento_iguais()
    {
        var hub = new HubFake();
        await hub.CriarAsync(new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "BENTO SEM CPF" }],
            BirthDate = "2024-01-01",
            Identifier = [new Identifier("urn:salux:cd_paciente", "salux-hcml:888")],
        });

        await RodarAsync(OrigemPadrao(), hub);

        // O homônimo do Salux continua lá; o do Klinikos entrou como OUTRO recurso.
        Assert.Equal(4, hub.Do<Patient>().Count);
        Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == "urn:salux:cd_paciente" && i.Value == "salux-hcml:888"));
        Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == "urn:klinikos:paciente" && i.Value == $"{Slug}:P2"));
    }

    /// <summary>
    /// O defeito do incidente: TODAS as escritas falharam e o ponteiro avançou até o fim — 1,43
    /// milhão de linhas invisíveis para sempre. Agora: falha segura o ponteiro, e o ciclo
    /// seguinte recupera tudo sozinho.
    /// </summary>
    [Fact]
    public async Task Falha_de_escrita_segura_o_ponteiro__e_o_ciclo_seguinte_recupera()
    {
        var hub = new HubFake
        {
            // P2 (rv 200) falha na primeira rodada.
            FalharSe = r => r is Patient pt && pt.Identifier.Any(i => i.Value == $"{Slug}:P2"),
        };
        var origem = OrigemPadrao();
        var marca = new MarcaDagua();

        var (_, p1, _) = await RodarAsync(origem, hub, marca);

        Assert.True(p1.FalhasTotal > 0);
        Assert.Equal(199, marca.Ponteiro("paciente"));       // min(falho−1, max) = 200−1
        Assert.Equal(1099, marca.Ponteiro("atendimento"));   // B2 depende de P2 → também falhou
        Assert.Equal(2, hub.Do<Patient>().Count);            // P1 e P3 entraram — falha não é barreira
        Assert.Equal(2, hub.Do<Encounter>().Count);

        // Ciclo seguinte, sem a falha: o keyset relê a partir do ponteiro segurado.
        hub.FalharSe = null;
        var (_, p2, _) = await RodarAsync(origem, hub, marca);

        Assert.Equal(0, p2.FalhasTotal);
        Assert.Equal(3, hub.Do<Patient>().Count);
        Assert.Equal(3, hub.Do<Encounter>().Count);
        Assert.Equal(300, marca.Ponteiro("paciente"));
        Assert.Equal(1200, marca.Ponteiro("atendimento"));
    }

    /// <summary>
    /// Mesmo CPF com nascimento diferente NÃO é sobrescrita — é divergência: congela o valor do
    /// hub e registra para a arbitragem. Já protegeu 5 pacientes reais no Salux (02/08).
    /// </summary>
    [Fact]
    public async Task Nascimento_divergente_no_mesmo_CPF_congela_e_registra_divergencia()
    {
        var hub = new HubFake();
        await hub.CriarAsync(new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA COM CPF" }],
            BirthDate = "1979-02-02",   // hub discorda da origem (1980-05-10)
            Identifier = [new Identifier(SysCpf, "52998224725")],
        });

        var (_, _, div) = await RodarAsync(OrigemPadrao(), hub);

        var ana = Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == SysCpf && i.Value == "52998224725"));
        Assert.Equal("1979-02-02", ana.BirthDate); // congelado: origem NÃO venceu

        var d = Assert.Single(div.Registradas);
        Assert.Equal("52998224725", d.Cpf);
        Assert.Equal("1980-05-10", d.ValorOrigem);
        Assert.Equal("1979-02-02", d.ValorHub);
    }

    /// <summary>
    /// Boletim importado ANTES de o atendimento começar entra como não-atendido; quando o
    /// INÍCIO chega num ciclo posterior, o status é curado — não fica errado para sempre.
    /// </summary>
    [Fact]
    public async Task INICIO_que_chega_depois_cura_o_status_do_boletim()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        origem.Evolucoes.RemoveAll(e => (string?)e["SPA_CODIGO"] == "B1"); // B1 ainda sem atendimento
        origem.Sinais.Clear();
        var marca = new MarcaDagua();

        await RodarAsync(origem, hub, marca);
        var b1 = Assert.Single(hub.Do<Encounter>(), e => e.Identifier.Any(i => i.Value == $"{Slug}:B1"));
        Assert.Equal(Encounter.EncounterStatus.Cancelled, b1.Status);

        // O atendimento começa: chega o INÍCIO com rv novo.
        origem.Evolucoes.Add(Evo(9, "B1", "INÍCIO DO ATENDIMENTO MÉDICO", "Início do Atendimento", "J06.9", 9000));
        await RodarAsync(origem, hub, marca);

        b1 = Assert.Single(hub.Do<Encounter>(), e => e.Identifier.Any(i => i.Value == $"{Slug}:B1"));
        Assert.Equal(Encounter.EncounterStatus.Finished, b1.Status);
    }

    /// <summary>Divergência já conhecida e descongelada ("origem correta") deixa a origem corrigir o hub.</summary>
    [Fact]
    public async Task Divergencia_ja_arbitrada_como_origem_correta_deixa_a_origem_corrigir()
    {
        var hub = new HubFake();
        await hub.CriarAsync(new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA COM CPF" }],
            BirthDate = "1979-02-02",
            Identifier = [new Identifier(SysCpf, "52998224725")],
        });

        // Arbitragem já disse: a origem está certa (congelar = false).
        var (_, _, div) = await RodarAsync(OrigemPadrao(), hub,
            conhecidas: new Dictionary<string, bool> { ["52998224725"] = false });

        var ana = Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == SysCpf && i.Value == "52998224725"));
        Assert.Equal("1980-05-10", ana.BirthDate);   // origem corrigiu
        Assert.Empty(div.Registradas);               // e não re-registra a mesma divergência
    }

    /// <summary>
    /// Escopo Limitado é ENSAIO: até N pacientes, só o clínico deles, e NENHUM ponteiro se
    /// move — um teste que avançasse a marca faria o incremental seguinte pular o resto da
    /// base. Foi exatamente por não existir isto que o primeiro run limitado teria importado
    /// a base inteira.
    /// </summary>
    [Fact]
    public async Task Escopo_limitado_e_um_ensaio__N_pacientes_so_o_clinico_deles_e_ponteiro_parado()
    {
        var hub = new HubFake();
        var progresso = new ProgressoImportacao();
        var marca = new MarcaDagua();
        var ctx = new ContextoImportacaoPep
        {
            Consulta = OrigemPadrao(),
            Opcoes = new OpcoesImportacao(
                ModoSincronizacao.Completo, EscopoSincronizacao.Limitado,
                MaxMedicos: 1, MaxPacientes: 1, ApagarAntes: false),
            Marca = marca,
            Escritor = hub,
            Progresso = progresso,
            BaseSlug = Slug,
        };
        await new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance)
            .ImportarAsync(ctx, CancellationToken.None);

        // Um paciente (P1, o primeiro por rv), um profissional, e só o clínico de P1.
        Assert.Single(hub.Do<Patient>());
        Assert.Single(hub.Do<Practitioner>());
        var enc = Assert.Single(hub.Do<Encounter>());
        Assert.Contains(enc.Identifier, i => i.Value == $"{Slug}:B1");
        Assert.Equal(0, progresso.FalhasTotal); // fora do escopo NÃO é falha

        // E os ponteiros não se moveram um milímetro.
        Assert.Equal(0, marca.Ponteiro("paciente"));
        Assert.Equal(0, marca.Ponteiro("atendimento"));
        Assert.Equal(0, marca.Ponteiro("evolucao"));
        Assert.Equal(0, marca.Ponteiro("sinais-vitais"));
    }

    /// <summary>Opção que o conector não implementa é REJEITADA na entrada, nunca ignorada.</summary>
    [Fact]
    public async Task Opcoes_nao_suportadas_sao_rejeitadas_na_entrada()
    {
        var estrategia = new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance);

        async Task Rodar(OpcoesImportacao opcoes) =>
            await estrategia.ImportarAsync(new ContextoImportacaoPep
            {
                Consulta = OrigemPadrao(),
                Opcoes = opcoes,
                Marca = new MarcaDagua(),
                Escritor = new HubFake(),
                Progresso = new ProgressoImportacao(),
                BaseSlug = Slug,
            }, CancellationToken.None);

        await Assert.ThrowsAsync<SMSMarica.Core.Common.Excecoes.ValidacaoException>(() =>
            Rodar(new OpcoesImportacao(ModoSincronizacao.Completo, EscopoSincronizacao.Tudo, null, null, ApagarAntes: true)));
        await Assert.ThrowsAsync<SMSMarica.Core.Common.Excecoes.ValidacaoException>(() =>
            Rodar(new OpcoesImportacao(ModoSincronizacao.Completo, EscopoSincronizacao.Tudo, null, null, false, CdsPacientes: [1, 2])));
    }

    // ---------------------------------------------------------------- achados da auditoria de 04/08

    /// <summary>
    /// PONTE local→CPF: paciente importado sem CPF ganha CPF na origem. Sem a segunda busca
    /// pela chave local, nasceria uma duplicata permanente com o histórico pendurado no
    /// recurso antigo. Com ela: MESMO recurso, agora com CPF — e a tag sai sozinha.
    /// </summary>
    [Fact]
    public async Task Paciente_que_ganha_CPF_depois_converge_para_o_MESMO_recurso()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        var marca = new MarcaDagua();

        // Rodada 1: P2 sem CPF.
        await RodarAsync(origem, hub, marca);
        var antes = Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == "urn:klinikos:paciente" && i.Value == $"{Slug}:P2"));
        Assert.Contains(antes.Meta!.Tag, t => t.Code == "identidade-incompleta");

        // A recepção corrige o cadastro: P2 ganha CPF válido (rv novo).
        var p2 = origem.Pacientes.Single(x => (string?)x["pac_codigo"] == "P2");
        p2["pac_cpf"] = "98765432100"; // CPF VÁLIDO — a régua de dígito verificador é real
        p2["rv"] = 5000L;
        await RodarAsync(origem, hub, marca);

        var depois = Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == "urn:klinikos:paciente" && i.Value == $"{Slug}:P2"));
        Assert.Equal(antes.Id, depois.Id); // MESMO recurso — nada duplicou
        Assert.Contains(depois.Identifier, i => i.System == SysCpf && i.Value == "98765432100");
        Assert.DoesNotContain(depois.Meta?.Tag ?? [], t => t.Code == "identidade-incompleta");
        Assert.Equal(3, hub.Do<Patient>().Count); // os mesmos 3 pacientes — nenhuma cópia nasceu
    }

    /// <summary>
    /// CPF-lixo ("00000000000" passa no teste de comprimento, não no de dígito verificador)
    /// NÃO ancora: duas pessoas com o mesmo CPF de preenchimento não podem fundir.
    /// </summary>
    [Fact]
    public async Task CPF_invalido_nao_ancora_nem_vira_identifier__duas_pessoas_nao_fundem()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        origem.Pacientes.Add(Pac("P8", "PESSOA UM", "00000000000", "1970-01-01", 400));
        origem.Pacientes.Add(Pac("P9", "PESSOA DOIS", "00000000000", "1970-01-01", 500));

        await RodarAsync(origem, hub, marca: null);

        // Cada uma é um recurso próprio, marcada, e SEM identifier de CPF.
        var um = Assert.Single(hub.Do<Patient>(), x => x.Identifier.Any(i => i.Value == $"{Slug}:P8"));
        var dois = Assert.Single(hub.Do<Patient>(), x => x.Identifier.Any(i => i.Value == $"{Slug}:P9"));
        Assert.NotEqual(um.Id, dois.Id);
        Assert.DoesNotContain(um.Identifier, i => i.System == SysCpf);
        Assert.Contains(um.Meta!.Tag, t => t.Code == "identidade-incompleta");
    }

    /// <summary>
    /// Fato clínico não some por omissão de outra base: óbito gravado pelo Salux sobrevive ao
    /// re-upsert do Klinikos que não traz óbito. Sem isso, morto "revivia" no hub.
    /// </summary>
    [Fact]
    public async Task Obito_gravado_por_uma_base_sobrevive_ao_upsert_da_outra()
    {
        var hub = new HubFake();
        await hub.CriarAsync(new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA COM CPF" }],
            BirthDate = "1980-05-10",
            Deceased = new FhirDateTime("2026-07-01"),
            Identifier = [new Identifier(SysCpf, "52998224725")],
        });

        await RodarAsync(OrigemPadrao(), hub); // o build do Klinikos NÃO traz óbito

        var ana = Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == SysCpf && i.Value == "52998224725"));
        Assert.Equal("2026-07-01", ((FhirDateTime)ana.Deceased!).Value);
    }

    /// <summary>
    /// Modo COMPLETO recua o ponteiro sobre falha abaixo da marca guardada — senão o registro
    /// falho ficaria atrás do corte do incremental, invisível para sempre.
    /// </summary>
    [Fact]
    public async Task Modo_completo_RECUA_o_ponteiro_quando_a_falha_esta_abaixo_da_marca()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        var marca = new MarcaDagua();
        marca.AvancarPonteiro("paciente", 9999); // marca antiga, adiante de tudo

        hub.FalharSe = r => r is Patient pt && pt.Identifier.Any(i => i.Value == $"{Slug}:P2"); // rv 200

        var progresso = new ProgressoImportacao();
        var ctx = new ContextoImportacaoPep
        {
            Consulta = origem,
            Opcoes = new OpcoesImportacao(ModoSincronizacao.Completo, EscopoSincronizacao.Tudo, null, null, false),
            Marca = marca,
            Escritor = hub,
            Progresso = progresso,
            BaseSlug = Slug,
        };
        await new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance)
            .ImportarAsync(ctx, CancellationToken.None);

        Assert.Equal(199, marca.Ponteiro("paciente")); // recuou: 9999 → 199 (falha em 200)
    }

    /// <summary>
    /// Atendido ≠ "passou pelo médico": boletim com evolução SÓ de enfermagem é atendimento
    /// real — marcá-lo "cancelled" seria status clínico falso (2.194 boletins na UPA).
    /// </summary>
    [Fact]
    public async Task Boletim_atendido_so_pela_enfermagem_e_finished__nao_cancelado()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        origem.Evolucoes.RemoveAll(e => (string?)e["SPA_CODIGO"] == "B1");
        origem.Evolucoes.Add(Evo(11, "B1", "EVOLUÇÃO DE ENFERMAGEM", "aferido e medicado", null, 2050));
        origem.Sinais.Clear();

        await RodarAsync(origem, hub);

        var b1 = Assert.Single(hub.Do<Encounter>(), e => e.Identifier.Any(i => i.Value == $"{Slug}:B1"));
        Assert.Equal(Encounter.EncounterStatus.Finished, b1.Status);
    }

    /// <summary>Valor absurdo não é medida: pulso 999 e "PA 12x8" não viram Observation final.</summary>
    [Fact]
    public async Task Vital_implausivel_nao_vira_Observation()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        origem.Sinais.Clear();
        origem.Sinais.Add(new()
        {
            ["sv_codigo"] = 78L, ["spa_codigo"] = "B1", ["data"] = "2026-08-01T10:25:00",
            ["prof_codigo"] = "0001", ["pressaoarterial"] = "12x8", ["pulso"] = "999",
            ["temperatura"] = "36.8", ["frequenciarespiratoria"] = null, ["hgt"] = null,
            ["saturacaoO2"] = null, ["peso"] = null, ["rv"] = 3100L,
        });

        await RodarAsync(origem, hub);

        var obs = hub.Do<Observation>();
        Assert.Single(obs); // só a temperatura sobreviveu
        Assert.Equal("8310-5", obs[0].Code.Coding[0].Code);
    }

    /// <summary>
    /// Rowversion ordena EDIÇÕES, não o curso clínico: a correção de texto numa evolução
    /// antiga (datahora velha, rv novo) não pode regredir o CID da reavaliação mais recente.
    /// </summary>
    [Fact]
    public async Task Edicao_de_evolucao_antiga_nao_regride_o_CID_da_reavaliacao()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        origem.Evolucoes.Clear();
        origem.Sinais.Clear();
        origem.Evolucoes.Add(Evo(21, "B1", "INÍCIO DO ATENDIMENTO MÉDICO", "início", "J06.9", 2000));
        var reav = Evo(22, "B1", "REAVALIAÇÃO", "quadro fechou", "J18.9", 2100);
        reav["upaevo_datahora"] = "2026-08-01T14:00:00";
        origem.Evolucoes.Add(reav);
        // Edição da linha ANTIGA (datahora 10:00) chega com rv MAIOR que a reavaliação.
        var edicao = Evo(21, "B1", "INÍCIO DO ATENDIMENTO MÉDICO", "início (texto corrigido)", "J06.9", 2200);
        edicao["upaevo_datahora"] = "2026-08-01T10:00:00";
        origem.Evolucoes.RemoveAll(e => (long)e["upaevo_codigo"]! == 21 && (long)e["rv"]! == 2000);
        origem.Evolucoes.Add(Evo(21, "B1", "INÍCIO DO ATENDIMENTO MÉDICO", "início", "J06.9", 2000));
        origem.Evolucoes.Add(edicao);

        await RodarAsync(origem, hub);

        var cond = Assert.Single(hub.Do<Condition>(), c => c.Identifier.Any(i => i.Value == $"{Slug}:B1:cond"));
        Assert.Equal("J18.9", cond.Code!.Coding[0].Code); // a reavaliação (14h) prevaleceu
    }

    /// <summary>
    /// "Apagar antes" purga a base inteira do hub antes de reescrever. Numa API que reinicia a
    /// cada deploy, uma purga interrompida deixa o prontuário incompleto SEM AVISO — e o upsert
    /// idempotente já reconcilia sem apagar nada. O conector recusa na entrada; o serviço
    /// recusa antes disso (Pep:PermitirApagarAntes, desligado por padrão); e a tela nem oferece.
    /// Três camadas porque o custo de errar aqui é prontuário sumido.
    /// </summary>
    [Fact]
    public async Task Apagar_antes_e_recusado_pelo_conector()
    {
        var estrategia = new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance);

        var ex = await Assert.ThrowsAsync<SMSMarica.Core.Common.Excecoes.ValidacaoException>(() =>
            estrategia.ImportarAsync(new ContextoImportacaoPep
            {
                Consulta = OrigemPadrao(),
                Opcoes = new OpcoesImportacao(
                    ModoSincronizacao.Completo, EscopoSincronizacao.Tudo, null, null, ApagarAntes: true),
                Marca = new MarcaDagua(),
                Escritor = new HubFake(),
                Progresso = new ProgressoImportacao(),
                BaseSlug = Slug,
            }, CancellationToken.None));

        Assert.Contains("Apagar antes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// O código do paciente na divergência tem de ser o da ORIGEM, tal e qual. O extrator
    /// antigo pegava "os dígitos" do identifier prefixado
    /// (<c>upa24h-marica-sqlserver:062608050044</c>) — capturava o <c>24</c> do próprio slug e
    /// perdia os zeros à esquerda, virando <c>24062608050044</c>. As 959 divergências da UPA
    /// nasceram assim, apontando para um paciente que não existe.
    /// </summary>
    [Fact]
    public async Task Divergencia_guarda_o_codigo_da_origem_tal_e_qual()
    {
        var hub = new HubFake();
        await hub.CriarAsync(new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA COM CPF" }],
            BirthDate = "1979-02-02",
            Identifier = [new Identifier(SysCpf, "52998224725")],
        });
        var origem = OrigemPadrao();
        // código com zeros à esquerda, como o Klinikos escreve de verdade
        origem.Pacientes.Single(x => (string?)x["pac_codigo"] == "P1")["pac_codigo"] = "062608050044";
        origem.Boletins.Single(b => (string?)b["spa_codigo"] == "B1")["pac_codigo"] = "062608050044";

        var (_, _, div) = await RodarAsync(origem, hub);

        var d = Assert.Single(div.Registradas);
        Assert.Equal("062608050044", d.CodigoOrigem);   // sem o "24" do slug, com os zeros
        Assert.Equal(62608050044L, d.CdPaciente);        // forma numérica, para o Salux
    }

    /// <summary>
    /// Reimport direcionado: a arbitragem devolve ao hub um paciente cuja origem foi declarada
    /// correta. Processa exatamente os códigos pedidos, com o clínico deles — e NÃO move
    /// ponteiro, porque é reparo pontual, fora do fluxo do CDC.
    /// </summary>
    [Fact]
    public async Task Reimport_direcionado_traz_so_os_codigos_pedidos_e_nao_move_ponteiro()
    {
        var hub = new HubFake();
        var marca = new MarcaDagua();
        var progresso = new ProgressoImportacao();
        var ctx = new ContextoImportacaoPep
        {
            Consulta = OrigemPadrao(),
            Opcoes = new OpcoesImportacao(
                ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo, null, null, false,
                CodigosPacientes: ["P3"]),
            Marca = marca,
            Escritor = hub,
            Progresso = progresso,
            BaseSlug = Slug,
        };
        await new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance)
            .ImportarAsync(ctx, CancellationToken.None);

        Assert.Equal(0, progresso.FalhasTotal);
        var pac = Assert.Single(hub.Do<Patient>());
        Assert.Contains(pac.Identifier, i => i.Value == $"{Slug}:P3");
        var enc = Assert.Single(hub.Do<Encounter>());
        Assert.Contains(enc.Identifier, i => i.Value == $"{Slug}:B3");

        Assert.Equal(0, marca.Ponteiro("paciente"));
        Assert.Equal(0, marca.Ponteiro("atendimento"));
    }

    /// <summary>Código NUMÉRICO nesta base é recusado: perderia os zeros e apontaria para outro paciente.</summary>
    [Fact]
    public async Task Reimport_por_codigo_numerico_e_recusado_nesta_base()
    {
        var estrategia = new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance);

        var ex = await Assert.ThrowsAsync<SMSMarica.Core.Common.Excecoes.ValidacaoException>(() =>
            estrategia.ImportarAsync(new ContextoImportacaoPep
            {
                Consulta = OrigemPadrao(),
                Opcoes = new OpcoesImportacao(
                    ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo, null, null, false,
                    CdsPacientes: [62608050044]),
                Marca = new MarcaDagua(),
                Escritor = new HubFake(),
                Progresso = new ProgressoImportacao(),
                BaseSlug = Slug,
            }, CancellationToken.None));

        Assert.Contains("zeros à esquerda", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Prontuário partido, medido em produção em 05/08: o paciente entra SEM CPF (registro pela
    /// chave local), a recepção preenche o CPF depois, e o ciclo seguinte resolve por CPF num
    /// registro que JÁ existia por OUTRA base — deixando o primeiro órfão, com parte do
    /// histórico. A ponte só cobria o caso em que a busca por CPF não achava nada.
    ///
    /// <para>Fundir dois recursos com clínica pendurada é cirurgia (repontar referências,
    /// remover o perdedor) e não se improvisa no caminho quente do upsert — mas TEM de ficar
    /// visível. Vira falha registrada.</para>
    /// </summary>
    [Fact]
    public async Task Prontuario_partido_entre_bases_vira_falha_registrada()
    {
        var hub = new HubFake();
        // (1) a UPA já tinha criado o paciente SEM CPF, pela chave local
        await hub.CriarAsync(new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA COM CPF" }],
            BirthDate = "1980-05-10",
            Identifier = [new Identifier("urn:klinikos:paciente", $"{Slug}:P1")],
        });
        // (2) e a MESMA pessoa já estava no hub pelo Salux, com o CPF
        await hub.CriarAsync(new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA COM CPF" }],
            BirthDate = "1980-05-10",
            Identifier =
            [
                new Identifier(SysCpf, "52998224725"),
                new Identifier("urn:salux:cd_paciente", "salux-hcml:777"),
            ],
        });

        // (3) agora a origem tem o CPF: resolve no registro do Salux e o local fica órfão
        var (_, p, _) = await RodarAsync(OrigemPadrao(), hub);

        Assert.Contains(p.Falhas, f => f.Mensagem.Contains("prontuário partido", StringComparison.OrdinalIgnoreCase)
                                    && f.Mensagem.Contains($"{Slug}:P1", StringComparison.Ordinal));
    }

    /// <summary>O total de falhas conta além do teto do detalhe — o detalhe é amostra, o número é exato.</summary>
    [Fact]
    public void Detalhe_de_falhas_e_amostra_mas_o_total_e_exato()
    {
        var p = new ProgressoImportacao();
        for (var i = 0; i < ProgressoImportacao.MaxDetalheFalhas + 700; i++)
            p.RegistrarFalha(i, "erro repetido");

        Assert.Equal(ProgressoImportacao.MaxDetalheFalhas + 700, p.FalhasTotal);
        Assert.Equal(ProgressoImportacao.MaxDetalheFalhas, p.Falhas.Count);
    }
}
