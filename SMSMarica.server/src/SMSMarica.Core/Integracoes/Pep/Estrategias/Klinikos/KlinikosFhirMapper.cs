using System.Globalization;
using System.Text;
using Hl7.Fhir.Model;

namespace SMSMarica.Core.Integracoes.Pep.Estrategias.Klinikos;

/// <summary>
/// Klinikos (SQL Server) → FHIR R4. Mapeamento medido em 03/08/2026 contra a instância da UPA
/// Maricá; a íntegra, com os números, está em <c>docs/klinikos/mapeamento-fhir.md</c>.
///
/// <para>Segue as mesmas regras do conector do Salux, porque o hub é o mesmo: Patient e
/// Practitioner são <b>canônicos</b> (dedup por chave nacional, feita fora daqui); os códigos
/// internos entram prefixados pelo slug da instância, para as duas UPAs não colidirem; e
/// <c>meta.source</c> diz de qual base veio cada recurso.</para>
///
/// <para><b>A diferença que mais pesa</b>: o CONTEÚDO clínico não tem tabela própria. O CID, a
/// nota e a prescrição saem todos de <c>UPA_Evolucao</c>, discriminados por <c>Tipo</c> — as
/// tabelas de registro clínico do módulo de emergência (<c>Resumo_Alta</c>,
/// <c>Evolucao_Diagnosticos</c>, <c>Atendimento</c>) estão vazias nesta implantação.</para>
///
/// <para>O que <b>não</b> está vazio é o esqueleto administrativo do atendimento:
/// <c>atendimento_ambulatorial</c> e <c>UPA_Atendimento_Medico</c> têm uma linha por boletim e
/// carregam o fechamento — quando saiu e por quê. É de onde vem o <c>period.end</c> do Encounter
/// (ver <c>DesfechoBoletim</c>).</para>
/// </summary>
internal sealed class KlinikosFhirMapper(string slug, string source)
{
    public const string SourceBase = "https://smsmarica.saude.marica/source";

    public string Source { get; } = source;

    private const string SysCpf = "https://fhir.saude.gov.br/sid/cpf";
    private const string SysCns = "https://fhir.saude.gov.br/sid/cns";
    private const string SysCnes = "https://fhir.saude.gov.br/sid/cnes";
    private const string SysConselho = "urn:br:conselho:";
    private const string SysCid = "http://hl7.org/fhir/sid/icd-10";
    private const string SysClass = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
    private const string SysLoinc = "http://loinc.org";
    private const string SysUcum = "http://unitsofmeasure.org";
    private const string SysObsCat = "http://terminology.hl7.org/CodeSystem/observation-category";
    private const string SysDischarge = "http://terminology.hl7.org/CodeSystem/discharge-disposition";

    private const string SysUnidade = "urn:klinikos:unidade";
    private const string SysTipoSaida = "urn:klinikos:tiposaida";
    private const string SysProfissional = "urn:klinikos:profissional";
    private const string SysPaciente = "urn:klinikos:paciente";
    private const string SysBoletim = "urn:klinikos:boletim";
    private const string SysEvolucao = "urn:klinikos:evolucao";
    private const string SysSinais = "urn:klinikos:sinaisvitais";

    public const string IdentCnes = SysCnes;
    public const string IdentUnidade = SysUnidade;
    public const string IdentProfissional = SysProfissional;
    public const string IdentPaciente = SysPaciente;
    public const string IdentBoletim = SysBoletim;
    public const string IdentEvolucao = SysEvolucao;
    public const string IdentSinais = SysSinais;
    public const string IdentCpf = SysCpf;
    public const string IdentCns = SysCns;

    private readonly string _slug = slug;

    /// <summary>Prefixa um código interno com o slug da instância — as duas UPAs usam os mesmos códigos.</summary>
    public string Pref(string valor) => $"{_slug}:{valor}";

    // ---------------- helpers ----------------

    private static string? S(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    /// <summary>Data-hora da origem + fuso de Brasília. O SQL Server guarda hora local, sem offset.</summary>
    private static string? Dt(string? v) => S(v) is { } s ? s + "-03:00" : null;

    private static string Dig(string? v) => string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
    private static bool Verdadeiro(string? v) => (v ?? string.Empty).Trim().ToUpperInvariant() is "S" or "1" or "T";

    private Meta Meta() => new() { Source = Source };

    /// <summary>
    /// Identifier com <c>value</c> ou <c>system</c> vazio faz o hub rejeitar o recurso INTEIRO
    /// com 400. Guarda geral, aplicada em todo recurso — foi assim que o paciente sem CPF
    /// nasceu quebrado no conector do Salux, e não vale repetir o erro na segunda base.
    /// </summary>
    private static List<Identifier> SemVazios(IEnumerable<Identifier> ids) =>
        [.. ids.Where(i => !string.IsNullOrWhiteSpace(i.Value) && !string.IsNullOrWhiteSpace(i.System))];

    private static AdministrativeGender Genero(string? sexo) => (sexo ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "M" => AdministrativeGender.Male,
        "F" => AdministrativeGender.Female,
        _ => AdministrativeGender.Unknown,
    };

    /// <summary>
    /// Telefone que vale a pena guardar. A recepção preenche o campo obrigatório com lixo
    /// quando o paciente não informa número — <c>0000000000</c> aparece no primeiro registro da
    /// base. Um telefone falso no hub é pior que telefone nenhum: alguém vai tentar ligar, e o
    /// contato entra em relatório de "paciente contactável" sem ser.
    /// </summary>
    private static string? Telefone(string? v)
    {
        var d = Dig(v);
        if (d.Length < 8) return null;
        if (d.Distinct().Count() == 1) return null;   // 0000000000, 9999999999…
        return S(v);
    }

    /// <summary>Só a data (o Klinikos guarda nascimento como datetime com hora zerada).</summary>
    private static string? SoData(string? v) =>
        S(v) is { } s && s.Length >= 10 ? s[..10] : null;

    // ---------------- Organization (ADR-0039) ----------------

    /// <summary>
    /// Unidade → <c>Organization</c>. O CNES entra como identificador NACIONAL: é ele que casa
    /// esta unidade com a MESMA unidade vista pelo Salux, cujo nome é outro ("UPA 24H INOÃ" lá,
    /// "UPA MARICA" aqui). Medido: os dois lados declaram 7164440.
    ///
    /// <para>O CNES está em <c>unid_codigoCNES</c>. Existe também <c>unidade_municipioCNES</c>,
    /// que está vazia — não é essa.</para>
    /// </summary>
    public Organization BuildOrganization(UnidadeLinha u)
    {
        var ident = new List<Identifier>();
        if (Dig(u.Cnes) is { Length: > 0 } cnes) ident.Add(new Identifier(SysCnes, cnes));
        ident.Add(new Identifier(SysUnidade, Pref(u.Codigo)));

        var org = new Organization
        {
            Meta = Meta(),
            Identifier = SemVazios(ident),
            Name = S(u.Nome) ?? S(u.Fantasia),
            Active = true,
        };

        var apelidos = new[] { S(u.Fantasia), S(u.Sigla) }
            .OfType<string>()
            .Where(a => !string.Equals(a, org.Name, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var a in apelidos) org.AliasElement.Add(new FhirString(a));

        if (S(u.Telefone) is { } tel)
            org.Telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Phone, null, tel));
        if (S(u.Email) is { } mail)
            org.Telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Email, null, mail));

        return org;
    }

    // ---------------- Practitioner ----------------

    /// <summary>
    /// Profissional a partir de <b>TODAS as linhas do mesmo <c>PROF_CODIGO</c></b>.
    ///
    /// <para>A tabela <c>profissional</c> do Klinikos não é uma linha por pessoa: é uma linha por
    /// <b>(pessoa × qualificação)</b>. Medido em prod 06/08/2026 na UPA: 496 linhas para 395
    /// profissionais; o código <c>2988</c> tem SETE linhas, mesmo nome e CPF, com sete CBOs e
    /// quatro números de conselho.</para>
    ///
    /// <para>Enquanto o conector tratava cada linha como uma pessoa, o efeito era duplo e os dois
    /// lados passavam despercebidos: o <c>qualification</c> era <b>sobrescrito</b> a cada linha —
    /// o hub guardava só o CBO da última e perdia os outros seis em silêncio — enquanto os
    /// identifiers de conselho <b>acumulavam</b> (o merge canônico faz o seu trabalho). Um campo
    /// atropelava, o outro somava. E cada linha era uma escrita: sete versões novas por ciclo,
    /// de 11 em 11 minutos, sempre terminando no mesmo estado final.</para>
    ///
    /// <para><c>Practitioner.qualification</c> é uma <b>lista</b> no FHIR exatamente para isto.</para>
    /// </summary>
    /// <param name="linhas">Linhas de um mesmo <c>PROF_CODIGO</c>; a identidade vem da primeira.</param>
    public Practitioner BuildPractitioner(IReadOnlyList<ProfissionalLinha> linhas)
    {
        var p = linhas[0];

        var ident = new List<Identifier>();
        if (Dig(p.Cpf) is { Length: > 0 } cpf) ident.Add(new Identifier(SysCpf, cpf));
        if (Dig(p.Cns) is { Length: > 0 } cns) ident.Add(new Identifier(SysCns, cns));
        // Um conselho por linha: entram TODOS, sem repetir, e NORMALIZADOS.
        foreach (var conselho in linhas.Select(l => ConselhoPep.Normalizar(l.Conselho)).OfType<string>().Distinct(StringComparer.Ordinal))
            ident.Add(new Identifier(SysConselho + "crm", conselho));
        ident.Add(new Identifier(SysProfissional, Pref(p.Codigo)));

        var pr = new Practitioner
        {
            Meta = Meta(),
            Identifier = SemVazios(ident),
            // Ativo se QUALQUER qualificação estiver ativa: o profissional que deixou de exercer
            // uma ocupação e continua em outra não pode sair do cadastro.
            Active = linhas.Any(l => l.Ativo is null || Verdadeiro(l.Ativo)),
        };
        if (S(p.Nome) is { } nome)
            pr.Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }];

        pr.Qualification = [.. linhas
            .Select(l => S(l.Cbo)).OfType<string>().Distinct(StringComparer.Ordinal)
            .Select(cbo => new Practitioner.QualificationComponent
            {
                Code = new CodeableConcept { Coding = [new Coding("urn:br:gov:cbo", cbo)] },
            })];
        return pr;
    }

    // ---------------- Patient ----------------

    /// <summary>
    /// Paciente. CPF é a âncora do merge canônico e <b>não é obrigatório</b>: 16,9% do cadastro
    /// da UPA (14.152 pessoas, todas com atendimento) não tem CPF nem CNS. Elas entram
    /// identificadas só pelo código interno e marcadas com a tag de identidade incompleta —
    /// ficam de fora do merge por CPF, porque não há como deduplicá-las com segurança.
    /// </summary>
    public Patient BuildPatient(PacienteLinha p)
    {
        // Só CPF VÁLIDO (dígito verificador) vira identifier de CPF: "00000000000" e CPF
        // digitado errado são preenchimento de campo obrigatório, não chave nacional — como
        // identifier, poluiriam a coluna de busca do hub; como âncora, FUNDIRIAM duas pessoas.
        var cpf = CpfPep.Valido(p.Cpf) ? p.CpfDigitos : string.Empty;
        var ident = new List<Identifier>();
        if (cpf.Length > 0) ident.Add(new Identifier(SysCpf, cpf));
        if (Dig(p.Cns) is { Length: > 0 } cns) ident.Add(new Identifier(SysCns, cns));
        ident.Add(new Identifier(SysPaciente, Pref(p.Codigo)));

        var pac = new Patient
        {
            Meta = Meta(),
            Identifier = SemVazios(ident),
            Gender = Genero(p.Sexo),
            BirthDate = SoData(p.Nascimento),
            Active = true,
        };
        if (S(p.Nome) is { } nome)
            pac.Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }];

        foreach (var tel in new[] { Telefone(p.Telefone), Telefone(p.Celular) }.OfType<string>().Distinct())
            pac.Telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Phone, null, tel));
        if (S(p.Email) is { } mail)
            pac.Telecom.Add(new ContactPoint(ContactPoint.ContactPointSystem.Email, null, mail));

        if (S(p.Mae) is { } mae)
        {
            pac.Contact = [new Patient.ContactComponent
            {
                Relationship = [new CodeableConcept("http://terminology.hl7.org/CodeSystem/v2-0131", "N", "mãe")],
                Name = new HumanName { Text = mae },
            }];
        }
        if (S(p.Responsavel) is { } resp)
        {
            var c = new Patient.ContactComponent { Name = new HumanName { Text = resp } };
            if (S(p.TelefoneResponsavel) is { } t)
                c.Telecom = [new ContactPoint(ContactPoint.ContactPointSystem.Phone, null, t)];
            (pac.Contact ??= []).Add(c);
        }

        if (SoData(p.Obito) is { } obito) pac.Deceased = new FhirDateTime(obito);

        if (cpf.Length == 0) MarcarIdentidadeIncompleta(pac); // sem CPF VÁLIDO = identidade incompleta
        return pac;
    }

    /// <summary>
    /// Marca o paciente sem CPF. Mesma tag do conector do Salux de propósito: a pergunta
    /// "quantos registros incertos temos?" tem de ter UMA resposta no hub inteiro, não uma por
    /// base — <c>GET /fhir/Patient?_tag=urn:smsmarica:qualidade|identidade-incompleta</c>.
    /// </summary>
    public static void MarcarIdentidadeIncompleta(Patient p)
    {
        p.Meta ??= new Meta();
        p.Meta.Tag ??= [];
        const string sys = "urn:smsmarica:qualidade";
        const string cod = "identidade-incompleta";
        if (p.Meta.Tag.Any(t => t.System == sys && t.Code == cod)) return;
        p.Meta.Tag.Add(new Coding(sys, cod) { Display = "Sem CPF — não é possível unir a outras bases" });
    }

    // ---------------- Encounter ----------------

    /// <summary>
    /// Boletim → <c>Encounter</c> classe EMER (é UPA 24h; não há atendimento eletivo aqui).
    ///
    /// <para><paramref name="teveAtendimento"/> decide o status: 92,3% dos boletins chegam a ter
    /// atendimento médico; o resto é evasão. <b>Os dois entram</b> — o paciente esteve na
    /// unidade, e isso é informação clínica. O que muda é o status, não a existência.</para>
    ///
    /// <para><b>Status não é estado de alta.</b> Quem precisa saber que a pessoa foi embora lê
    /// <c>period.end</c>: <c>finished</c> aqui significa "teve atendimento", e é gravado desde a
    /// primeira evolução — enquanto o paciente ainda está na unidade.</para>
    /// </summary>
    public Encounter BuildEncounter(
        BoletimLinha b, string patientRef, string? organizationRef, bool teveAtendimento,
        DesfechoBoletim? desfecho = null)
    {
        var enc = new Encounter
        {
            Meta = Meta(),
            Status = teveAtendimento
                ? Encounter.EncounterStatus.Finished
                : Encounter.EncounterStatus.Cancelled,
            Class = new Coding(SysClass, "EMER", "emergency"),
            Subject = new ResourceReference(patientRef),
            Identifier = [new Identifier(SysBoletim, Pref(b.Codigo))],
        };
        if (organizationRef is not null) enc.ServiceProvider = new ResourceReference(organizationRef);

        if ((Dt(b.Chegada) ?? Dt(b.DataBoletim)) is { } inicio)
        {
            enc.Period = new Period { Start = inicio };
            if (Dt(desfecho?.Fim) is { } f) enc.Period.End = f;
        }

        if (DischargeDoTipoSaida(desfecho) is { } alta)
            enc.Hospitalization = new Encounter.HospitalizationComponent { DischargeDisposition = alta };

        return enc;
    }

    /// <summary>
    /// <c>Tipo_Saida</c> do Klinikos (17 códigos) → ValueSet <c>discharge-disposition</c> do R4.
    /// O código da origem vai junto, como segunda <c>Coding</c>, e a descrição original no
    /// <c>text</c>: o R4 tem 11 códigos genéricos e a origem distingue coisas que importam aqui
    /// (evasão antes × depois do médico, encaminhamento à rede básica × à emergência). Traduzir
    /// só para o padrão perderia o que decide se cabe pesquisa de satisfação.
    /// </summary>
    private static CodeableConcept? DischargeDoTipoSaida(DesfechoBoletim? d)
    {
        if (d?.TipoSaida is not { } cod) return null;

        // Medido na UPA em 30 dias (08/08/2026): 9.820 do código 17, 385 do 3, 101 do 12,
        // 67 do 1, 31 do 5, 12 do 6, 3 do 2, 1 do 8. O resto do catálogo é cauda.
        var (padrao, display) = cod switch
        {
            1 or 10 or 14 or 17 => ("home", "Home"),              // alta para casa, com ou sem encaminhamento
            2 => ("aadvice", "Left against advice"),               // alta a pedido
            3 or 12 => ("aadvice", "Left against advice"),         // evasão (o R4 não tem "eloped")
            4 or 5 or 7 or 11 or 15 => ("other-hcf", "Other healthcare facility"),
            6 or 8 => ("exp", "Expired"),                          // óbito / chegou cadáver
            _ => ("oth", "Other"),                                 // 9 extraviado, 13/16 baixa administrativa
        };

        var cc = new CodeableConcept { Text = S(d.TipoSaidaDs) };
        cc.Coding =
        [
            new Coding(SysDischarge, padrao, display),
            new Coding(SysTipoSaida, cod.ToString(CultureInfo.InvariantCulture), S(d.TipoSaidaDs)),
        ];
        return cc;
    }

    // ---------------- Condition ----------------

    /// <summary>
    /// CID da evolução → <c>Condition</c>. O identifier é do BOLETIM (não da linha de evolução):
    /// a reavaliação muda o CID do mesmo atendimento, e uma Condition por evolução encheria o
    /// prontuário de diagnósticos concorrentes para uma passagem só.
    /// </summary>
    public Condition? BuildCondition(string boletim, string? cid, string patientRef, string encRef)
    {
        if (S(cid) is not { } bruto) return null;
        var codigo = LimparCodigoCid(bruto);
        var code = new CodeableConcept { Text = bruto };
        if (codigo is not null) code.Coding = [new Coding(SysCid, codigo)];

        return new Condition
        {
            Meta = Meta(),
            Subject = new ResourceReference(patientRef),
            Encounter = new ResourceReference(encRef),
            Code = code,
            Identifier = [new Identifier(SysBoletim, Pref(boletim) + ":cond")],
        };
    }

    /// <summary>CID-10 cruz-estrela: a origem grava "L14 *"; o tipo <c>code</c> do FHIR não aceita espaço nem asterisco.</summary>
    private static string? LimparCodigoCid(string? cid)
    {
        var s = S(cid);
        if (s is null) return null;
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '.') sb.Append(char.ToUpperInvariant(c));
            else break;
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    // ---------------- Observation (sinais vitais) ----------------

    private static readonly (string Campo, string Loinc, string Nome, string Unidade)[] Vitais =
    [
        ("pulso", "8867-4", "Frequência cardíaca", "/min"),
        ("temperatura", "8310-5", "Temperatura corporal", "Cel"),
        ("frequenciaRespiratoria", "9279-1", "Frequência respiratória", "/min"),
        ("saturacaoO2", "2708-6", "Saturação de O2", "%"),
        ("peso", "29463-7", "Peso corporal", "kg"),
        ("hgt", "2339-0", "Glicemia capilar", "mg/dL"),
    ];

    /// <summary>
    /// Sinais vitais → uma <c>Observation</c> por medida presente. Aqui as colunas já vêm
    /// separadas (o Salux entrega EAV), então não há adivinhação de qual valor é o quê.
    /// Só entra o que tem número: campo em branco não vira Observation vazia.
    /// </summary>
    public List<(string Chave, Observation Obs)> BuildObservacoesVitais(
        SinaisVitaisLinha sv, string patientRef, string encRef)
    {
        var obs = new List<(string, Observation)>();
        var eff = Dt(sv.Data);

        foreach (var (campo, loinc, nome, unidade) in Vitais)
        {
            var bruto = campo switch
            {
                "pulso" => sv.Pulso,
                "temperatura" => sv.Temperatura,
                "frequenciaRespiratoria" => sv.FrequenciaRespiratoria,
                "saturacaoO2" => sv.SaturacaoO2,
                "peso" => sv.Peso,
                _ => sv.Hgt,
            };
            if (Plausivel(campo, Decimal(bruto)) is not { } valor) continue;
            obs.Add((ChaveVital(sv, campo), Observacao(sv, patientRef, encRef, eff, loinc, nome, campo, valor, unidade)));
        }

        // Pressão arterial vem como texto ("120/80") — vira um painel com dois componentes,
        // que é como o FHIR representa PA. Guardar "120/80" numa string perderia a medida.
        if (Pressao(sv.PressaoArterial) is { } pa)
        {
            var o = Observacao(sv, patientRef, encRef, eff, "85354-9", "Pressão arterial", "pa", null, null);
            o.Component =
            [
                Componente("8480-6", "Sistólica", pa.Sis),
                Componente("8462-4", "Diastólica", pa.Dia),
            ];
            obs.Add((ChaveVital(sv, "pa"), o));
        }
        return obs;
    }

    /// <summary>Identifier determinístico da medida: uma Observation por (linha de sinais, campo).</summary>
    private string ChaveVital(SinaisVitaisLinha sv, string campo) =>
        Pref(sv.Codigo.ToString(CultureInfo.InvariantCulture)) + ":" + campo;

    private Observation Observacao(
        SinaisVitaisLinha sv, string patientRef, string encRef, string? eff,
        string loinc, string nome, string sufixo, decimal? valor, string? unidade)
    {
        var o = new Observation
        {
            Meta = Meta(),
            Status = ObservationStatus.Final,
            Category = [new CodeableConcept(SysObsCat, "vital-signs", "Vital Signs")],
            Code = new CodeableConcept(SysLoinc, loinc, nome),
            Subject = new ResourceReference(patientRef),
            Encounter = new ResourceReference(encRef),
            Identifier = [new Identifier(SysSinais, ChaveVital(sv, sufixo))],
        };
        if (eff is not null) o.Effective = new FhirDateTime(eff);
        if (valor is { } v)
            o.Value = new Quantity { Value = v, Unit = unidade, System = SysUcum, Code = unidade };
        return o;
    }

    private static Observation.ComponentComponent Componente(string loinc, string nome, decimal valor) => new()
    {
        Code = new CodeableConcept(SysLoinc, loinc, nome),
        Value = new Quantity { Value = valor, Unit = "mm[Hg]", System = SysUcum, Code = "mm[Hg]" },
    };

    private static decimal? Decimal(string? v)
    {
        var s = S(v)?.Replace(',', '.');
        if (s is null) return null;
        // Descarta o que não é medida ("N/A", "-", "não aferido").
        return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0
            ? d : null;
    }

    /// <summary>
    /// Faixas de PLAUSIBILIDADE por medida — deliberadamente largas (só matam o absurdo):
    /// pulso 999, "PA 12x8" (shorthand de digitação) e peso 0 não são medidas, são erro de
    /// digitação — e uma Observation <c>final</c> com valor absurdo é informação clínica
    /// FALSA no prontuário. Adivinhar o valor pretendido seria pior: inventaríamos dado.
    /// </summary>
    private static readonly Dictionary<string, (decimal Min, decimal Max)> Faixas = new()
    {
        ["pulso"] = (10, 350),
        ["temperatura"] = (25, 46),
        ["frequenciaRespiratoria"] = (2, 120),
        ["saturacaoO2"] = (10, 100),
        ["hgt"] = (5, 2000),
        ["peso"] = (0.3m, 600),
    };

    private static decimal? Plausivel(string campo, decimal? valor) =>
        valor is { } v && Faixas.TryGetValue(campo, out var f) && v >= f.Min && v <= f.Max
            ? v : valor is { } fora && !Faixas.ContainsKey(campo) ? fora : null;

    private static (decimal Sis, decimal Dia)? Pressao(string? v)
    {
        var s = S(v);
        if (s is null) return null;
        var partes = s.Split(['/', 'x', 'X'], StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length != 2) return null;
        if (Decimal(partes[0]) is not { } sis || Decimal(partes[1]) is not { } dia) return null;
        // "12x8" é shorthand de digitação, não uma pressão de 12/8 mmHg.
        if (sis is < 30 or > 350 || dia is < 10 or > 250 || dia >= sis) return null;
        return (sis, dia);
    }

    // ---------------- DocumentReference ----------------

    /// <summary>
    /// Evolução narrativa → <c>DocumentReference</c>. Uma por linha de evolução; o
    /// <c>type.text</c> preserva o rótulo original da origem ("EVOLUÇÃO DE ENFERMAGEM"), que é
    /// mais informativo para quem lê o prontuário do que um código genérico.
    /// </summary>
    public DocumentReference BuildDocRef(EvolucaoLinha e, string patientRef, string encRef)
    {
        var texto = S(e.Descricao) ?? string.Empty;
        return new DocumentReference
        {
            Meta = Meta(),
            Status = DocumentReferenceStatus.Current,
            Type = new CodeableConcept { Text = S(e.Tipo) ?? "Evolução" },
            Subject = new ResourceReference(patientRef),
            // DateTimeOffset.Parse preserva o -03:00 que o Dt() põe; DateTime.Parse converteria
            // para o fuso da MÁQUINA — e o servidor roda em UTC, não em Brasília.
            Date = Dt(e.DataHora) is { } d
                ? DateTimeOffset.Parse(d, CultureInfo.InvariantCulture)
                : null,
            Identifier =
            [
                new Identifier(SysEvolucao, Pref(e.Codigo.ToString(CultureInfo.InvariantCulture))),
            ],
            Context = new DocumentReference.ContextComponent { Encounter = [new ResourceReference(encRef)] },
            Content =
            [
                new DocumentReference.ContentComponent
                {
                    Attachment = new Attachment
                    {
                        ContentType = "text/plain",
                        Data = Encoding.UTF8.GetBytes(texto),
                        Title = S(e.Tipo),
                    },
                },
            ],
        };
    }

    // ---------------- MedicationRequest ----------------

    /// <summary>
    /// Receita/prescrição → <c>MedicationRequest</c>. Nesta implantação a prescrição é
    /// <b>texto livre</b> na evolução, então o medicamento vai em
    /// <c>medicationCodeableConcept.text</c>, sem <c>Dosage</c> estruturado. Prometer estrutura
    /// que a origem não tem seria inventar dado clínico.
    /// </summary>
    public MedicationRequest BuildMedicationRequest(EvolucaoLinha e, string patientRef, string encRef) => new()
    {
        Meta = Meta(),
        Status = MedicationRequest.MedicationrequestStatus.Completed,
        Intent = MedicationRequest.MedicationRequestIntent.Order,
        Medication = new CodeableConcept { Text = S(e.Descricao) ?? S(e.Tipo) ?? "Prescrição" },
        Subject = new ResourceReference(patientRef),
        Encounter = new ResourceReference(encRef),
        AuthoredOnElement = Dt(e.DataHora) is { } d ? new FhirDateTime(d) : null,
        Identifier =
        [
            new Identifier(SysEvolucao, Pref(e.Codigo.ToString(CultureInfo.InvariantCulture)) + ":med"),
        ],
    };
}
