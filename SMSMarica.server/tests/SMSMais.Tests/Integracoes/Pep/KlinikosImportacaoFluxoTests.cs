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

namespace SMSMais.Tests.Integracoes.Pep;

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

        /// <summary>
        /// Fechamento do boletim (<c>atendimento_ambulatorial</c> + <c>UPA_Atendimento_Medico</c>).
        /// Vazio por padrão: o boletim ainda aberto é o caso normal do incremental, e é o estado
        /// em que a maioria dos testes deste arquivo lê a origem.
        /// </summary>
        public List<Dictionary<string, object?>> Desfechos { get; } = [];
        public List<Dictionary<string, object?>> Evolucoes { get; } = [];
        public List<Dictionary<string, object?>> Sinais { get; } = [];

        /// <summary>Narrativa do atendimento (<c>UPA_Atendimento_Medico</c>) — o boletim médico.</summary>
        public List<Dictionary<string, object?>> BoletinsMedicos { get; } = [];

        /// <summary>Itens de medicamento prescrito (<c>Item_Prescricao_Medicamento</c> + <c>Prescricao</c>).</summary>
        public List<Dictionary<string, object?>> ItensPrescricao { get; } = [];

        /// <summary>Catálogo <c>TB_CID</c>. Sem rowversion no contrato: é varredura integral.</summary>
        public List<Dictionary<string, object?>> Cids { get; } = [];

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

            // OFFSET/FETCH — como o catálogo CID pagina (não dá para keyset numa tabela de
            // domínio sem rowversion útil).
            if (Regex.Match(sql, @"OFFSET\s+(\d+)\s+ROWS\s+FETCH\s+NEXT\s+(\d+)\s+ROWS",
                    RegexOptions.IgnoreCase) is { Success: true } o)
            {
                linhas = [.. linhas.Skip(int.Parse(o.Groups[1].Value)).Take(int.Parse(o.Groups[2].Value))];
            }

            // O AGENTE CORTA no teto e não avisa — é assim em produção, e é o que fez o
            // catálogo CID voltar com 5.000 de 14.242 códigos na primeira subida. Um fake que
            // devolve tudo esconde exatamente essa classe de bug.
            if (maxLinhasOverride is { } teto && linhas.Count > teto) linhas = [.. linhas.Take(teto)];

            return Task.FromResult(Tabela(linhas));
        }

        private List<Dictionary<string, object?>> TabelaDe(string sql) => sql switch
        {
            _ when sql.Contains("FROM unidade", StringComparison.OrdinalIgnoreCase) => Unidades,
            _ when sql.Contains("FROM profissional", StringComparison.OrdinalIgnoreCase) => Profissionais,
            _ when sql.Contains("FROM paciente", StringComparison.OrdinalIgnoreCase) => Pacientes,
            _ when sql.Contains("FROM Pronto_Atendimento", StringComparison.OrdinalIgnoreCase) => Boletins,
            // `FROM <tabela>` e não só o nome: `SqlDesfechos` faz LEFT JOIN em
            // UPA_Atendimento_Medico e `SqlBoletinsMedicos` faz JOIN em atendimento_ambulatorial
            // — casar pelo nome solto trocaria uma consulta pela outra.
            _ when sql.Contains("FROM UPA_Atendimento_Medico", StringComparison.OrdinalIgnoreCase) => BoletinsMedicos,
            _ when sql.Contains("FROM Item_Prescricao_Medicamento", StringComparison.OrdinalIgnoreCase) => ItensPrescricao,
            _ when sql.Contains("FROM TB_CID", StringComparison.OrdinalIgnoreCase) => Cids,
            _ when sql.Contains("FROM atendimento_ambulatorial", StringComparison.OrdinalIgnoreCase) => Desfechos,
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

        /// <summary>
        /// <c>meta.versionId</c> que chegou em cada PUT — é o If-Match da concorrência otimista.
        /// Fica registrado para provar que a guarda de no-op devolve o campo que ela zera para
        /// comparar: perdê-lo desligaria a proteção da edição do painel em silêncio.
        /// </summary>
        public List<string?> VersoesRecebidasNoUpdate { get; } = [];

        public Task<Resource> AtualizarAsync(string tipo, string id, Resource recurso, CancellationToken ct = default)
        {
            Falha(recurso);
            VersoesRecebidasNoUpdate.Add(recurso.Meta?.VersionId);
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
        // O rótulo é TUDO que a origem grava nessas linhas — nada de medicamento. Elas não
        // podem virar MedicationRequest; a prescrição de verdade está em ItensPrescricao.
        f.Evolucoes.Add(Evo(2, "B1", "RECEITA", "Receita", null, 2100));
        f.Evolucoes.Add(Evo(3, "B3", "EVOLUÇÃO MÉDICA", "paciente estável, mantém conduta", null, 2200));
        f.Evolucoes.Add(Evo(4, "B3", "ESTORNO", "lançamento anulado", null, 2300));
        f.Evolucoes.Add(Evo(5, "B3", "INÍCIO DO ATENDIMENTO MÉDICO", "Início do Atendimento", "A90", 2400));
        f.Evolucoes.Add(Evo(6, "B1", "REAVALIAÇÃO", "Reavaliação", "J06.9", 2500));

        f.BoletinsMedicos.Add(BolMed("A1", "B1", "dor de garganta há 3 dias", "orofaringe hiperemiada",
            "faringite aguda", "sintomáticos e retorno se piora", 4000));
        f.ItensPrescricao.Add(ItemPresc("11111111-1111-1111-1111-111111111111", "PR1", "B1",
            "DipiRONA 500 mg/ml SOLUÇÃO INJETÁVEL 2ml AMPOLA", 1m, "ampola", "INTRAMUSCULAR", 360, 5000));
        f.Cids.Add(new() { ["CO_CID"] = "J069", ["NO_CID"] = "INFECCAO AGUDA DAS VIAS AEREAS SUPERIORES NE" });
        f.Cids.Add(new() { ["CO_CID"] = "A90", ["NO_CID"] = "DENGUE" });
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

    /// <summary>Linha de fechamento — o rowversion é o da <c>atendimento_ambulatorial</c>, não o do boletim.</summary>
    private static Dictionary<string, object?> Desf(string spa, string fim, int? tipsai, string? tipsaiDs, long rv) => new()
    {
        ["spa_codigo"] = spa, ["atendamb_datafinal"] = fim, ["tipsai_codigo"] = tipsai,
        ["tipsai_Descricao"] = tipsaiDs, ["prof_codigo_encerramento"] = "0001", ["rv"] = rv,
    };

    private static Dictionary<string, object?> Evo(long cod, string spa, string tipo, string desc, string? cid, long rv) => new()
    {
        ["upaevo_codigo"] = cod, ["SPA_CODIGO"] = spa, ["Tipo"] = tipo,
        ["upaevo_datahora"] = "2026-08-01T10:00:00", ["upaevo_descricao"] = desc,
        ["prof_codigo"] = "0001", ["cid_codigo_primario"] = cid, ["cid_codigo_secundario"] = null,
        ["rv"] = rv,
    };

    /// <summary>Linha de <c>UPA_Atendimento_Medico</c> — a narrativa do atendimento.</summary>
    private static Dictionary<string, object?> BolMed(
        string atend, string spa, string? anamnese, string? exame, string? hipotese, string? conduta, long rv) => new()
    {
        ["atendamb_codigo"] = atend, ["spa_codigo"] = spa,
        ["atendamb_datainicio"] = "2026-08-01T09:40:00",
        ["upaatemed_Anamnese"] = anamnese, ["upaatemed_ExameFisico"] = exame,
        ["upaatemed_HipoteseDiagnostica"] = hipotese, ["upaatemed_ProcedimentoProposto"] = conduta,
        ["upaatemed_Observacao"] = null, ["prof_codigo_encerramento"] = "0001", ["rv"] = rv,
    };

    /// <summary>Item de medicamento prescrito — o remédio de verdade, com dose e via.</summary>
    private static Dictionary<string, object?> ItemPresc(
        string id, string presc, string spa, string insumo, decimal qtd, string unidade,
        string via, int frequencia, long rv) => new()
    {
        ["item_id"] = id, ["presc_codigo"] = presc, ["spa_codigo"] = spa,
        ["presc_data"] = "2026-08-01T11:00:00", ["prof_codigo"] = "0001",
        ["ins_descricao"] = insumo, ["itpresc_quantidade"] = qtd, ["ins_unidade"] = unidade,
        ["viamed_descricao"] = via, ["itpresc_frequencia"] = frequencia,
        ["itpresc_duracao"] = 3, ["itpresc_qtd_sos"] = null, ["rv"] = rv,
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

        // Condition por boletim; ESTORNO, INÍCIO e REAVALIAÇÃO não viram documento.
        Assert.Equal(2, hub.Do<Condition>().Count);        // B1 (J06.9) e B3 (A90)

        // O medicamento vem da PRESCRIÇÃO, não da evolução: um item, com o nome do remédio.
        var med = Assert.Single(hub.Do<MedicationRequest>());
        Assert.Equal("DipiRONA 500 mg/ml SOLUÇÃO INJETÁVEL 2ml AMPOLA", (med.Medication as CodeableConcept)?.Text);

        // Dois documentos: a evolução médica de B3 e o boletim médico de B1.
        var docs = hub.Do<DocumentReference>();
        Assert.Equal(2, docs.Count);
        var evolucao = Assert.Single(docs, d => d.Type?.Text == "EVOLUÇÃO MÉDICA");
        Assert.Contains("mantém conduta", System.Text.Encoding.UTF8.GetString(evolucao.Content[0].Attachment.Data!));

        // Sinais: PA (painel) + pulso = 2 Observations.
        Assert.Equal(2, hub.Do<Observation>().Count);

        // Ponteiros no máximo de cada fase — nada falhou.
        Assert.Equal(0, p.FalhasTotal);
        Assert.Equal(300, ctx.Marca.Ponteiro("paciente"));
        Assert.Equal(1200, ctx.Marca.Ponteiro("atendimento"));
        Assert.Equal(2500, ctx.Marca.Ponteiro("evolucao"));
        Assert.Equal(3000, ctx.Marca.Ponteiro("sinais-vitais"));
        Assert.Equal(4000, ctx.Marca.Ponteiro("boletim-medico"));
        Assert.Equal(5000, ctx.Marca.Ponteiro("prescricao"));
    }

    /// <summary>
    /// O buraco que esta frente fechou: a narrativa do atendimento — anamnese, exame físico,
    /// hipótese e conduta — vive em <c>UPA_Atendimento_Medico</c>, e o conector lia essa tabela
    /// só para pegar o tipo de saída. O prontuário do Klinikos no hub não tinha boletim médico.
    /// </summary>
    [Fact]
    public async Task Boletim_medico_entra_como_documento_com_as_quatro_secoes()
    {
        var hub = new HubFake();
        await RodarAsync(OrigemPadrao(), hub);

        var doc = Assert.Single(hub.Do<DocumentReference>(), d => d.Type?.Text == "Boletim de Atendimento Médico");
        var html = System.Text.Encoding.UTF8.GetString(doc.Content[0].Attachment.Data!);

        Assert.Contains("Anamnese", html, StringComparison.Ordinal);
        Assert.Contains("dor de garganta há 3 dias", html, StringComparison.Ordinal);
        Assert.Contains("Exame físico", html, StringComparison.Ordinal);
        Assert.Contains("orofaringe hiperemiada", html, StringComparison.Ordinal);
        Assert.Contains("Hipótese diagnóstica", html, StringComparison.Ordinal);
        Assert.Contains("faringite aguda", html, StringComparison.Ordinal);
        Assert.Contains("Conduta", html, StringComparison.Ordinal);
        Assert.Contains("sintomáticos e retorno se piora", html, StringComparison.Ordinal);

        // Um documento por ATENDIMENTO: o médico edita o mesmo registro durante a passagem, e
        // cada edição tem de reescrever o boletim — não empilhar cópias no prontuário.
        Assert.Contains(doc.Identifier, i => i.System == "urn:klinikos:atendimento-medico" && i.Value == $"{Slug}:A1");
        Assert.Equal("text/html", doc.Content[0].Attachment.ContentType);

        // Documento SEM data aparece no prontuário sem quando e estraga a ordenação. Os
        // primeiros 168.450 boletins entraram assim em produção porque o parâmetro estava
        // preparado e recebia null — e nenhum teste olhava para ele.
        var quando = Assert.IsType<DateTimeOffset>(doc.Date);
        Assert.Equal(new DateTime(2026, 8, 1, 9, 40, 0), quando.DateTime);
        Assert.Equal(TimeSpan.FromHours(-3), quando.Offset);   // Brasília, não o fuso do servidor
    }

    /// <summary>
    /// O profissional existe no hub com nome, CPF e conselho — mas o prontuário mostrava só a
    /// data do atendimento, porque nada apontava para ele. Agora o médico entra no
    /// <c>Encounter.participant</c> (é de lá que a tela lê o nome), no <c>author</c> do boletim
    /// e no <c>requester</c> da prescrição.
    /// </summary>
    [Fact]
    public async Task Medico_do_atendimento_e_ligado_ao_Encounter_ao_boletim_e_a_prescricao()
    {
        var f = OrigemPadrao();
        f.Desfechos.Add(Desf("B1", "2026-08-01T12:00:00", 17, "alta", 1500));

        var hub = new HubFake();
        await RodarAsync(f, hub);

        var prof = Assert.Single(hub.Do<Practitioner>());
        var esperado = $"Practitioner/{prof.Id}";
        Assert.Equal("DR HOUSE", prof.Name[0].Text);

        var enc = Assert.Single(hub.Do<Encounter>(), e => e.Identifier.Any(i => i.Value == $"{Slug}:B1"));
        Assert.Equal(esperado, enc.Participant[0].Individual?.Reference);

        var doc = Assert.Single(hub.Do<DocumentReference>(), d => d.Type?.Text == "Boletim de Atendimento Médico");
        Assert.Equal(esperado, doc.Author?[0].Reference);

        var med = Assert.Single(hub.Do<MedicationRequest>());
        Assert.Equal(esperado, med.Requester?.Reference);
    }

    /// <summary>
    /// Profissional sem CPF não vira Practitioner (regra canônica) — e o atendimento dele entra
    /// do mesmo jeito, sem autor. Autoria é enriquecimento: nunca pode custar o registro clínico.
    /// </summary>
    [Fact]
    public async Task Medico_que_nao_resolve_nao_impede_o_atendimento_de_entrar()
    {
        var f = OrigemPadrao();
        // 0002 é o profissional SEM CPF do cenário: fica fora do hub por regra canônica.
        f.Desfechos.Add(Desf("B1", "2026-08-01T12:00:00", 17, "alta", 1500));
        f.Desfechos[0]["prof_codigo_encerramento"] = "0002";

        var hub = new HubFake();
        var (_, p, _) = await RodarAsync(f, hub);

        var enc = Assert.Single(hub.Do<Encounter>(), e => e.Identifier.Any(i => i.Value == $"{Slug}:B1"));
        Assert.Empty(enc.Participant);
        Assert.Equal(0, p.FalhasTotal);
    }

    /// <summary>
    /// Boletim médico aberto e ainda sem nada escrito não vira documento vazio no prontuário —
    /// e o ponteiro avança mesmo assim, porque não há nada a recuperar num ciclo seguinte.
    /// </summary>
    [Fact]
    public async Task Boletim_medico_sem_narrativa_nao_vira_documento_vazio()
    {
        var f = OrigemPadrao();
        f.BoletinsMedicos.Clear();
        f.BoletinsMedicos.Add(BolMed("A1", "B1", null, null, null, null, 4000));

        var hub = new HubFake();
        var (ctx, p, _) = await RodarAsync(f, hub);

        Assert.DoesNotContain(hub.Do<DocumentReference>(), d => d.Type?.Text == "Boletim de Atendimento Médico");
        Assert.Equal(0, p.FalhasTotal);
        Assert.Equal(4000, ctx.Marca.Ponteiro("boletim-medico"));
    }

    /// <summary>
    /// A regressão que mais doeu no prontuário: a prescrição saía de <c>UPA_Evolucao</c>, cuja
    /// <c>upaevo_descricao</c> guarda o RÓTULO da linha — o hub ficou com 100% dos
    /// MedicationRequest do Klinikos dizendo "Receita" ou "Prescrição", sem nenhum medicamento.
    /// </summary>
    [Fact]
    public async Task Evolucao_tipo_RECEITA_nao_vira_medicamento_chamado_Receita()
    {
        var hub = new HubFake();
        await RodarAsync(OrigemPadrao(), hub);

        var textos = hub.Do<MedicationRequest>()
            .Select(m => (m.Medication as CodeableConcept)?.Text)
            .ToList();

        Assert.DoesNotContain("Receita", textos);
        Assert.DoesNotContain("Prescrição", textos);
        var med = Assert.Single(hub.Do<MedicationRequest>());
        Assert.Contains("DipiRONA", (med.Medication as CodeableConcept)?.Text, StringComparison.Ordinal);

        // A posologia é montada do que a origem tem de fato: 360 minutos são 6 em 6 horas.
        Assert.Equal("1 ampola · via intramuscular · de 6/6h · por 3 dia(s)", med.DosageInstruction?.FirstOrDefault()?.Text);
        Assert.Contains(med.Identifier, i => i.System == "urn:klinikos:prescricao");
    }

    /// <summary>
    /// REAVALIAÇÃO é evento, não narrativa: <c>upaevo_descricao</c> traz sempre a palavra
    /// "Reavaliação" (11 bytes). Enquanto virava DocumentReference, respondia por 85% dos
    /// documentos do Klinikos no hub — ruído puro em cima do prontuário.
    /// </summary>
    [Fact]
    public async Task Reavaliacao_nao_vira_documento__mas_ainda_revisa_o_CID()
    {
        var hub = new HubFake();
        await RodarAsync(OrigemPadrao(), hub);

        Assert.DoesNotContain(hub.Do<DocumentReference>(), d => d.Type?.Text == "REAVALIAÇÃO");

        // O que a reavaliação carrega de útil — o CID do boletim — continua entrando.
        Assert.Contains(hub.Do<Condition>(), c => c.Code?.Coding?.Any(x => x.Code == "J06.9") == true);
    }

    /// <summary>
    /// O "M545 · M545" da tela: sem o catálogo, o <c>text</c> da Condition repetia o código.
    /// A descrição vem de <c>TB_CID</c> e o código sai no formato canônico, com ponto.
    /// </summary>
    [Fact]
    public async Task CID_ganha_descricao_do_catalogo_e_codigo_com_ponto()
    {
        var hub = new HubFake();
        await RodarAsync(OrigemPadrao(), hub);

        var dengue = Assert.Single(hub.Do<Condition>(), c => c.Code?.Coding?.Any(x => x.Code == "A90") == true);
        Assert.Equal("DENGUE", dengue.Code?.Text);

        // "J06.9" na origem e "J069" no catálogo são o MESMO código — a chave ignora o ponto.
        var ivas = Assert.Single(hub.Do<Condition>(), c => c.Code?.Coding?.Any(x => x.Code == "J06.9") == true);
        Assert.Equal("INFECCAO AGUDA DAS VIAS AEREAS SUPERIORES NE", ivas.Code?.Text);
    }

    /// <summary>
    /// O catálogo é PAGINADO. A primeira versão pediu <c>TB_CID</c> inteira numa consulta só e
    /// o agente devolveu 5.000 das 14.242 linhas, sem avisar — 65% dos diagnósticos ficariam sem
    /// nome, e em silêncio. Aqui a origem tem 6.000 códigos e o teto do agente é o real.
    /// </summary>
    [Fact]
    public async Task Catalogo_CID_maior_que_o_teto_do_agente_vem_INTEIRO()
    {
        var f = OrigemPadrao();
        f.Cids.Clear();
        // Prefixo "A0000"… porque ordinalmente '0' < '9': todos ficam ANTES de "A90", que assim
        // cai na terceira página. Se a paginação parar na primeira resposta do agente (5.000),
        // esta descrição não chega — que é exatamente o que aconteceu em produção.
        for (var i = 0; i < 6_000; i++)
            f.Cids.Add(new() { ["CO_CID"] = $"A{i:D4}", ["NO_CID"] = $"DIAGNOSTICO {i}" });
        f.Cids.Add(new() { ["CO_CID"] = "A90", ["NO_CID"] = "DENGUE" });
        f.Cids.Sort((a, b) => string.CompareOrdinal((string)a["CO_CID"]!, (string)b["CO_CID"]!));

        var hub = new HubFake();
        await RodarAsync(f, hub);

        var cond = Assert.Single(hub.Do<Condition>(), c => c.Code?.Coding?.Any(x => x.Code == "A90") == true);
        Assert.Equal("DENGUE", cond.Code?.Text);
    }

    /// <summary>
    /// Catálogo indisponível não derruba o run: a Condition ainda entra, com o código no lugar
    /// da descrição. Enriquecimento que falha não pode custar dado clínico.
    /// </summary>
    [Fact]
    public async Task Sem_catalogo_CID_a_Condition_ainda_entra_com_o_codigo()
    {
        var f = OrigemPadrao();
        f.Cids.Clear();

        var hub = new HubFake();
        var (_, p, _) = await RodarAsync(f, hub);

        var cond = Assert.Single(hub.Do<Condition>(), c => c.Code?.Coding?.Any(x => x.Code == "A90") == true);
        Assert.Equal("A90", cond.Code?.Text);
        Assert.Equal(0, p.FalhasTotal);
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

    /// <summary>
    /// A alta chega num ciclo POSTERIOR e o boletim não muda — é o caso normal, não a exceção.
    /// Medido na UPA em 08/08/2026: dos 2.350 boletins fechados em 7 dias, zero tiveram o
    /// <c>rv_atualizacao</c> do <c>Pronto_Atendimento</c> avançado. Se o fechamento dependesse
    /// do CDC do boletim, o hub registraria a chegada de todo mundo e a saída de ninguém.
    /// </summary>
    [Fact]
    public async Task Alta_lancada_depois_fecha_o_Encounter_mesmo_sem_o_boletim_mudar()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        var marca = new MarcaDagua();

        await RodarAsync(origem, hub, marca);
        var b1 = Assert.Single(hub.Do<Encounter>(), e => e.Identifier.Any(i => i.Value == $"{Slug}:B1"));
        Assert.Null(b1.Period!.End);          // ainda na unidade
        Assert.Null(b1.Hospitalization);

        // Paciente vai embora. Só a `atendimento_ambulatorial` é tocada — o boletim fica intacto.
        origem.Desfechos.Add(Desf("B1", "2026-08-01T15:30:00", 17, "A.1 - Atendimento em consultório concluído", 5000));
        await RodarAsync(origem, hub, marca);

        b1 = Assert.Single(hub.Do<Encounter>(), e => e.Identifier.Any(i => i.Value == $"{Slug}:B1"));
        Assert.StartsWith("2026-08-01T15:30:00", b1.Period!.End, StringComparison.Ordinal);
        Assert.Equal("home", b1.Hospitalization!.DischargeDisposition!.Coding
            .Single(c => c.System == "http://terminology.hl7.org/CodeSystem/discharge-disposition").Code);
        Assert.Equal(5000, marca.Ponteiro("fechamento"));

        // E não nasceu Encounter novo: o upsert por identifier reaproveitou o mesmo recurso.
        Assert.Equal(3, hub.Do<Encounter>().Count);
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

    /// <summary>
    /// Interrupção no meio de uma fase (deploy, queda) não pode custar a fase inteira. O
    /// ponteiro é salvo a cada PÁGINA — em 05/08 três cargas morreram por deploy e cada
    /// retomada relia a fase do zero, 78 minutos de evoluções repetidos.
    /// </summary>
    [Fact]
    public async Task Ponteiro_e_salvo_a_cada_pagina__interrupcao_nao_custa_a_fase_inteira()
    {
        var hub = new HubFake();
        var marca = new MarcaDagua();
        var salvos = new List<long>();

        var ctx = new ContextoImportacaoPep
        {
            Consulta = OrigemPadrao(),
            Opcoes = new OpcoesImportacao(ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo, null, null, false),
            Marca = marca,
            Escritor = hub,
            Progresso = new ProgressoImportacao(),
            BaseSlug = Slug,
            SalvarMarca = (m, _) =>
            {
                salvos.Add(m.Ponteiro("paciente"));
                return Task.CompletedTask;
            },
        };
        await new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance)
            .ImportarAsync(ctx, CancellationToken.None);

        // Salvou durante a varredura, não só no fim — e sempre para a frente.
        Assert.NotEmpty(salvos);
        Assert.Equal(salvos.OrderBy(x => x), salvos);
        Assert.Equal(300, marca.Ponteiro("paciente"));
    }

    // ---------------------------------------------------------------- guarda de no-op (06/08)

    /// <summary>
    /// Ciclo que relê a mesma pessoa sem mudança NÃO escreve. O incremental relê um bloco fixo
    /// de gente todo poll de propósito (internação em curso é re-lida a cada ciclo — ADR-0025;
    /// médicos são re-scan integral), e antes da guarda cada poll gravava um PUT idêntico ao
    /// anterior: em produção, pacientes internados no HMCML chegaram a <c>version_id</c> 226.
    /// </summary>
    [Fact]
    public async Task Ciclo_repetido_sem_mudanca_nao_reescreve_a_pessoa_no_hub()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();

        await RodarAsync(origem, hub);
        var versoesApos1 = hub.Do<Patient>().Select(x => x.Meta?.VersionId).ToList();

        // Marca nova de novo: força o ciclo a reler exatamente as mesmas pessoas.
        var (_, p2, _) = await RodarAsync(origem, hub);

        Assert.Equal(3, hub.Do<Patient>().Count);                    // nada duplicou
        Assert.Equal(versoesApos1, hub.Do<Patient>().Select(x => x.Meta?.VersionId));
        Assert.All(hub.Do<Patient>(), x => Assert.Equal("1", x.Meta?.VersionId));
        Assert.Equal("1", Assert.Single(hub.Do<Practitioner>()).Meta?.VersionId);

        // E o run diz quantas releituras descartou, separando por cartão do painel.
        Assert.Equal(3, p2.PacientesInalterados);
        Assert.Equal(1, p2.MedicosInalterados);
        Assert.Empty(hub.VersoesRecebidasNoUpdate);                  // nenhum PUT saiu
    }

    /// <summary>
    /// A guarda não pode virar cegueira: mudança de verdade na origem continua escrevendo — e
    /// com o <c>versionId</c> intacto no PUT, que é o If-Match que protege a edição do painel.
    /// </summary>
    [Fact]
    public async Task Mudanca_real_na_origem_ainda_escreve__com_If_Match_intacto()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();

        await RodarAsync(origem, hub);

        // A recepção corrige o nome de P3.
        origem.Pacientes.Single(x => (string?)x["pac_codigo"] == "P3")["pac_nome"] = "CARLA COM CPF CORRIGIDA";
        var (_, p2, _) = await RodarAsync(origem, hub);

        var carla = Assert.Single(hub.Do<Patient>(), x => x.Identifier.Any(i => i.Value == $"{Slug}:P3"));
        Assert.Equal("CARLA COM CPF CORRIGIDA", carla.Name[0].Text);
        Assert.Equal("2", carla.Meta?.VersionId);                    // escreveu, uma vez só

        Assert.Equal(2, p2.PacientesInalterados);                    // os outros dois não mudaram
        Assert.Equal(1, p2.MedicosInalterados);
        var versao = Assert.Single(hub.VersoesRecebidasNoUpdate);
        Assert.Equal("1", versao);                                   // If-Match preservado pela guarda
    }

    /// <summary>
    /// Pessoa que existe em MAIS DE UMA base não pode ficar em ping-pong de identifiers.
    ///
    /// <para>Cada mapper monta os identifiers dele primeiro e a união anexa o resto no fim, então
    /// a ordem final dependia de qual base escreveu por último — a UPA gravava numa ordem, a
    /// Santa Rita regravava na outra, para sempre, e a guarda de no-op via diferença onde não
    /// havia mudança. Medido em prod 06/08: profissionais com o mesmo system repetido reescritos
    /// em 100% dos ciclos, um deles em version_id 585.</para>
    ///
    /// <para>A ordem canônica é o que fecha isso: se toda base produz a MESMA lista a partir do
    /// mesmo conjunto, não importa quem escreve por último — o resultado converge.</para>
    /// </summary>
    [Fact]
    public async Task Pessoa_em_duas_bases_sai_com_identifiers_em_ordem_canonica_e_para_de_ser_reescrita()
    {
        var hub = new HubFake();
        // O Salux já gravou esta pessoa, na ordem DELE (chave local antes do CPF).
        await hub.CriarAsync(new Patient
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "ANA COM CPF" }],
            BirthDate = "1980-05-10",
            Identifier =
            [
                new Identifier("urn:salux:cd_paciente", "salux-hcml:777"),
                new Identifier(SysCpf, "52998224725"),
            ],
        });
        var origem = OrigemPadrao();

        await RodarAsync(origem, hub);   // 1º ciclo do Klinikos: funde e regrava ordenado

        var ana = Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == SysCpf && i.Value == "52998224725"));
        Assert.Equal(
            ["https://fhir.saude.gov.br/sid/cpf", "urn:klinikos:paciente", "urn:salux:cd_paciente"],
            ana.Identifier.Select(i => i.System));
        var versaoAposFusao = ana.Meta?.VersionId;

        // 2º ciclo: nada mudou na origem — e agora a ordem também não muda.
        var (_, p2, _) = await RodarAsync(origem, hub);

        ana = Assert.Single(hub.Do<Patient>(), x =>
            x.Identifier.Any(i => i.System == SysCpf && i.Value == "52998224725"));
        Assert.Equal(versaoAposFusao, ana.Meta?.VersionId);
        Assert.Equal(3, p2.PacientesInalterados);   // os 3 pacientes, nenhum reescrito
    }

    /// <summary>
    /// Vários identifiers sob o MESMO system (o caso que reescrevia 100% dos ciclos: profissional
    /// com um CRM por base) também sai ordenado — o desempate é pelo valor.
    /// </summary>
    [Fact]
    public async Task Identifiers_repetindo_o_mesmo_system_desempatam_pelo_valor()
    {
        var hub = new HubFake();
        await hub.CriarAsync(new Practitioner
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "DR HOUSE" }],
            Identifier =
            [
                new Identifier("urn:br:conselho:crm", "99999"),
                new Identifier("urn:br:conselho:crm", "11111"),
                new Identifier(SysCpf, "39053344705"),
            ],
        });

        await RodarAsync(OrigemPadrao(), hub);

        var house = Assert.Single(hub.Do<Practitioner>());
        var crms = house.Identifier.Where(i => i.System == "urn:br:conselho:crm").Select(i => i.Value).ToList();
        Assert.Equal(crms.OrderBy(v => v, StringComparer.Ordinal), crms);
        // e o bloco de CRMs é contíguo (ordenado por system antes do valor)
        var sistemas = house.Identifier.Select(i => i.System).ToList();
        Assert.Equal(sistemas.OrderBy(s => s, StringComparer.Ordinal), sistemas);
    }

    /// <summary>
    /// Paciente resolvido pelo BOLETIM (não veio na fase de cadastro porque a linha dele não
    /// mudou) também entra no contador. Ficava invisível, o que subnotificava o trabalho do run
    /// e — depois que passou a existir <c>PacientesInalterados</c>, contado dentro do upsert
    /// canônico e portanto nos DOIS caminhos — fazia "alterados = total − inalterados" dar
    /// NEGATIVO no painel. Visto em prod 06/08 na Santa Rita: total 2, inalterados 5.
    /// </summary>
    [Fact]
    public async Task Paciente_resolvido_por_boletim_conta__inalterados_nunca_passa_do_total()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        var marca = new MarcaDagua();
        marca.AvancarPonteiro("paciente", 300);   // cadastro já visto: só os boletins são novos

        var (_, p, _) = await RodarAsync(origem, hub, marca);

        Assert.Equal(3, hub.Do<Patient>().Count);   // entraram pelo caminho do boletim
        Assert.Equal(3, p.Pacientes);
        Assert.True(p.Pacientes >= p.PacientesInalterados,
            $"inalterados ({p.PacientesInalterados}) não pode passar do total ({p.Pacientes})");
    }

    /// <summary>
    /// A tabela <c>profissional</c> é uma linha por <b>(pessoa × qualificação)</b>, não por
    /// pessoa. Medido em prod 06/08/2026 na UPA: 496 linhas para 395 profissionais; o código
    /// 2988 tem SETE linhas, mesmo nome e CPF, sete CBOs e quatro conselhos.
    ///
    /// <para>Tratando linha a linha, o <c>qualification</c> era sobrescrito a cada uma — o hub
    /// guardava só o CBO da última e perdia os outros seis em silêncio — e cada linha era uma
    /// versão nova (sete por ciclo, de 11 em 11 minutos). Agrupado: UMA escrita, TODAS as
    /// qualificações.</para>
    /// </summary>
    [Fact]
    public async Task Profissional_com_varias_linhas_vira_UM_recurso_com_TODAS_as_qualificacoes()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        origem.Profissionais.Clear();
        // Mesma pessoa em 3 linhas: 3 CBOs, 2 conselhos (um repetido, um vazio).
        foreach (var (cbo, conselho) in new[] { ("225125", "52123"), ("223208", "21424"), ("411010", (string?)null) })
        {
            var l = Prof("0001", "DR HOUSE", "39053344705");
            l["CBO_CODIGO"] = cbo;
            l["PROF_NUMCONSELHO"] = conselho;
            origem.Profissionais.Add(l);
        }

        var (_, p, _) = await RodarAsync(origem, hub);

        var house = Assert.Single(hub.Do<Practitioner>());
        Assert.Equal("1", house.Meta?.VersionId);   // UMA escrita, não três

        var cbos = house.Qualification
            .Select(q => q.Code.Coding[0].Code).OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(["223208", "225125", "411010"], cbos);   // nenhuma qualificação perdida

        var conselhos = house.Identifier
            .Where(i => i.System == "urn:br:conselho:crm").Select(i => i.Value)
            .OrderBy(x => x, StringComparer.Ordinal);
        Assert.Equal(["21424", "52123"], conselhos);

        Assert.Equal(1, p.Medicos);   // conta PESSOA, não linha
    }

    /// <summary>
    /// Re-scan de profissional é gated como no Salux: cadastro muda raramente e reler ~500 linhas
    /// a cada ciclo de 11 min é desperdício. O scheduler já calculava <c>ForcarMedicos</c> —
    /// faltava o conector consumir.
    /// </summary>
    [Fact]
    public async Task Rescan_de_profissional_so_roda_na_primeira_vez_ou_quando_forcado()
    {
        var hub = new HubFake();
        var origem = OrigemPadrao();
        var marca = new MarcaDagua();

        await RodarAsync(origem, hub, marca);                  // 1ª vez: marca nula → roda
        Assert.Single(hub.Do<Practitioner>());
        Assert.NotNull(marca.ProfissionalEm);

        // Ciclo seguinte: a marca está fresca e ninguém forçou → nem consulta a origem.
        origem.Profissionais.Add(Prof("0009", "DR NOVO", "11144477735"));
        var (_, p2, _) = await RodarAsync(origem, hub, marca);
        Assert.Equal(0, p2.Medicos);
        Assert.Single(hub.Do<Practitioner>());                 // o novo NÃO entrou ainda

        // Scheduler decide que a marca envelheceu → força, e aí sim entra.
        var progresso = new ProgressoImportacao();
        var ctx = new ContextoImportacaoPep
        {
            Consulta = origem,
            Opcoes = new OpcoesImportacao(ModoSincronizacao.Incremental, EscopoSincronizacao.Tudo,
                null, null, false, ForcarMedicos: true),
            Marca = marca,
            Escritor = hub,
            Progresso = progresso,
            BaseSlug = Slug,
        };
        await new KlinikosImportacaoStrategy(NullLogger<KlinikosImportacaoStrategy>.Instance)
            .ImportarAsync(ctx, CancellationToken.None);
        Assert.Equal(2, hub.Do<Practitioner>().Count);
    }

    /// <summary>
    /// Número de conselho vem de campo LIVRE na origem. Medido no hub em 06/08/2026: 13
    /// profissionais com <c>52137902-8</c>, <c>52.137338-8</c>, <c>52 1341308</c>,
    /// <c>&amp;nbsp;</c>. A régua NORMALIZA, não descarta — a maioria é registro real só mal
    /// formatado, e jogar fora perderia dado profissional legítimo. Some só o que não deixa
    /// dígito nenhum.
    ///
    /// <para>E o lixo que JÁ está no hub não é arrastado adiante: o merge deixa de copiar
    /// conselho fora da forma canônica, então o re-scan cura sozinho — mesmo padrão que já
    /// valia para CPF inválido.</para>
    /// </summary>
    [Fact]
    public async Task Conselho_e_normalizado_e_o_lixo_ja_gravado_nao_sobrevive_ao_merge()
    {
        var hub = new HubFake();
        // O que já está no hub: mascarado e lixo puro.
        await hub.CriarAsync(new Practitioner
        {
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = "DR HOUSE" }],
            Identifier =
            [
                new Identifier(SysCpf, "39053344705"),
                new Identifier("urn:br:conselho:crm", "52.137338-8"),
                new Identifier("urn:br:conselho:crm", "&nbsp;"),
            ],
        });

        var origem = OrigemPadrao();
        origem.Profissionais.Clear();
        var l = Prof("0001", "DR HOUSE", "39053344705");
        l["PROF_NUMCONSELHO"] = "52 1341308";   // formatado na origem
        origem.Profissionais.Add(l);

        await RodarAsync(origem, hub);

        var house = Assert.Single(hub.Do<Practitioner>());
        var conselhos = house.Identifier.Where(i => i.System == "urn:br:conselho:crm").Select(i => i.Value).ToList();
        Assert.Equal(["521341308"], conselhos);   // normalizado; mascarado e &nbsp; não sobreviveram
    }

    [Theory]
    [InlineData("52137902-8", "521379028")]
    [InlineData("52.137338-8", "521373388")]
    [InlineData("52 1341308", "521341308")]
    [InlineData("821.756", "821756")]
    [InlineData("&nbsp;", null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("12", null)]          // abaixo do piso: não é número de conselho
    public void Regua_do_conselho(string bruto, string? esperado) =>
        Assert.Equal(esperado, SMSMarica.Core.Integracoes.Pep.ConselhoPep.Normalizar(bruto));

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
