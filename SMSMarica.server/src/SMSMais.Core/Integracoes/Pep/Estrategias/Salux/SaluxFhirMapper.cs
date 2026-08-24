using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Hl7.Fhir.Model;

namespace SMSMais.Core.Integracoes.Pep.Estrategias.Salux;

/// <summary>
/// Constrói recursos FHIR R4 a partir das linhas do Salux — porta fiel dos <c>build_*</c>
/// de <c>Salux/scripts/importar_*_fhir.py</c>.
///
/// Multi-base (ADR-0009): Paciente/Practitioner são <b>canônicos</b> (dedup por chave nacional —
/// CPF/CNS/conselho — fora desta classe). Os códigos INTERNOS do Salux (cd_paciente, cd_medico,
/// baa, edoc) são <b>prefixados pelo slug da base</b> para não colidir entre instâncias e
/// carregar rastreabilidade. <see cref="Source"/> (meta.source) identifica a origem (PEP/base)
/// de cada recurso — usado também pra purgar clínica escopada por base.
/// </summary>
internal sealed class SaluxFhirMapper(string slug, string source)
{
    /// <summary>Prefixo base do meta.source; a estratégia compõe com tipo/slug.</summary>
    public const string SourceBase = "https://smsmarica.saude.marica/source";

    public string Source { get; } = source;

    private const string SysCpf = "https://fhir.saude.gov.br/sid/cpf";
    private const string SysCns = "https://fhir.saude.gov.br/sid/cns";
    private const string SysRg = "urn:br:gov:rg";
    private const string SysPis = "urn:br:gov:pis-pasep";
    private const string SysPass = "urn:passport";
    private const string SysRne = "urn:br:gov:rne";
    private const string SysCert = "urn:br:gov:certidao-nascimento";
    private const string SysSgh = "urn:sgh:prontuario";
    private const string SysCem = "urn:cem:prontuario";
    private const string SysSaluxPac = "urn:salux:cd_paciente";
    private const string SysSaluxMed = "urn:salux:cd_medico";
    private const string SysConselho = "urn:br:conselho:";
    private const string ExtrasUrl = "urn:salux:extras";
    private const string SysV3Role = "http://terminology.hl7.org/CodeSystem/v3-RoleCode";
    private const string SysCid = "http://hl7.org/fhir/sid/icd-10";
    private const string SysClass = "http://terminology.hl7.org/CodeSystem/v3-ActCode";
    private const string SysLoinc = "http://loinc.org";
    private const string SysUcum = "http://unitsofmeasure.org";
    private const string SysObsCat = "http://terminology.hl7.org/CodeSystem/observation-category";
    private const string SysBaa = "urn:salux:baa";
    private const string SysEdoc = "urn:salux:edoc";
    private const string SysPresc = "urn:salux:presc";
    private const string SysMatmed = "urn:salux:matmed";
    private const string SysRisco = "urn:salux:classificacao-risco";

    // Internação (ADR-0025)
    private const string SysHospital = "urn:salux:hospital";
    private const string SysCnes = "https://fhir.saude.gov.br/sid/cnes";
    private const string SysFia = "urn:salux:fia";
    private const string SysUnidade = "urn:salux:unidade";
    private const string SysQuarto = "urn:salux:quarto";
    private const string SysLeito = "urn:salux:leito";
    private const string SysPriority = "http://terminology.hl7.org/CodeSystem/v3-ActPriority";
    private const string SysDischarge = "http://terminology.hl7.org/CodeSystem/discharge-disposition";
    private const string SysLocPhysType = "http://terminology.hl7.org/CodeSystem/location-physical-type";

    // Sistemas de chave NACIONAL (globais) — chave de dedup canônica de Patient/Practitioner.
    public const string IdentCpf = SysCpf;
    public const string IdentCns = SysCns;
    /// <summary>System do código interno do paciente no Salux (valor é prefixado pelo slug).</summary>
    public const string IdentSaluxPaciente = SysSaluxPac;

    /// <summary>Systems dos identifiers determinísticos dos recursos clínicos (valores prefixados pelo slug).</summary>
    public const string IdentSaluxBaa = SysBaa;
    public const string IdentSaluxEdoc = SysEdoc;
    public const string IdentSaluxPresc = SysPresc;
    public const string IdentSaluxFia = SysFia;
    /// <summary>System do código interno da UNIDADE no Salux (ADR-0039).</summary>
    public const string IdentSaluxHospital = SysHospital;
    /// <summary>CNES — identificador NACIONAL da unidade; é a ponte entre PEPs diferentes.</summary>
    public const string IdentCnes = SysCnes;
    public const string IdentSaluxUnidade = SysUnidade;
    public const string IdentSaluxQuarto = SysQuarto;
    public const string IdentSaluxLeito = SysLeito;

    private readonly string _slug = slug;

    /// <summary>Prefixa um código interno do Salux com o slug da base (unicidade + rastreio).</summary>
    public string Pref(string valor) => $"{_slug}:{valor}";

    /// <summary>Extrai o código nativo de um identifier prefixado por este slug (ou null se for de outra base).</summary>
    public string? DesprefixarPaciente(string? valor) =>
        valor is not null && valor.StartsWith(_slug + ":", StringComparison.Ordinal)
            ? valor[(_slug.Length + 1)..]
            : null;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Dictionary<string, string> UfIbge = new()
    {
        ["11"] = "RO", ["12"] = "AC", ["13"] = "AM", ["14"] = "RR", ["15"] = "PA", ["16"] = "AP", ["17"] = "TO",
        ["21"] = "MA", ["22"] = "PI", ["23"] = "CE", ["24"] = "RN", ["25"] = "PB", ["26"] = "PE", ["27"] = "AL",
        ["28"] = "SE", ["29"] = "BA", ["31"] = "MG", ["32"] = "ES", ["33"] = "RJ", ["35"] = "SP",
        ["41"] = "PR", ["42"] = "SC", ["43"] = "RS", ["50"] = "MS", ["51"] = "MT", ["52"] = "GO", ["53"] = "DF",
    };

    // ---------------- helpers ----------------

    private static string? S(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static string? Dt(string? v) => S(v) is { } s ? s + "-03:00" : null;
    private static string Dig(string? v) => string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
    private static bool Verdadeiro(string? v) => (v ?? string.Empty).Trim().ToUpperInvariant() is "S" or "1" or "A" or "T";

    private static decimal? Num(string? v)
    {
        var s = S(v);
        if (s is null) return null;
        if (!decimal.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return null;
        return d == 0 ? null : d;
    }

    private static (decimal? Sist, decimal? Diast) ParsePa(string? txt)
    {
        var t = (S(txt) ?? string.Empty).Replace("x", "/").Replace("X", "/");
        var partes = t.Split('/');
        return partes.Length >= 2 ? (Num(partes[0]), Num(partes[1])) : (Num(t), null);
    }

    private Meta Meta() => new() { Source = Source };

    /// <summary>
    /// Descarta identifiers sem valor. Rede de segurança, não conserto pontual: UM identifier
    /// com <c>value</c> vazio faz o hub rejeitar o RECURSO INTEIRO com 400
    /// (<c>'' is not a correct literal for a string</c>) — o paciente simplesmente não entra.
    /// Aconteceu em 03/08 com o CPF, e a mesma armadilha existe para RG, PIS, passaporte e
    /// qualquer campo opcional que a origem devolva em branco.
    /// </summary>
    private static List<Identifier> SemVazios(IEnumerable<Identifier> ids) =>
        [.. ids.Where(i => !string.IsNullOrWhiteSpace(i.Value) && !string.IsNullOrWhiteSpace(i.System))];

    /// <summary>System da tag de qualidade do dado (buscável por <c>?_tag=</c>).</summary>
    public const string SysQualidade = "urn:smsmarica:qualidade";

    /// <summary>Código da tag: paciente sem identificador nacional (CPF).</summary>
    public const string TagIdentidadeIncompleta = "identidade-incompleta";

    /// <summary>
    /// Marca o Patient como de IDENTIDADE INCOMPLETA — sem CPF, portanto impossível de casar
    /// com o mesmo cidadão em outra base. Usa <c>meta.tag</c>, que é o mecanismo do próprio
    /// FHIR para qualificar o registro sem sujar o dado clínico, e é buscável:
    /// <c>GET /fhir/Patient?_tag=urn:smsmarica:qualidade|identidade-incompleta</c> responde
    /// "quantos registros incertos existem" a qualquer momento, sem contador paralelo.
    ///
    /// <para>Idempotente: aplicar de novo não duplica a tag. E some sozinha — no dia em que a
    /// origem ganhar o CPF, o paciente passa pelo caminho canônico e sobe sem a marca.</para>
    /// </summary>
    public static void MarcarIdentidadeIncompleta(Patient p)
    {
        p.Meta ??= new Meta();
        p.Meta.Tag ??= [];
        if (p.Meta.Tag.Any(t => t.System == SysQualidade && t.Code == TagIdentidadeIncompleta)) return;
        p.Meta.Tag.Add(new Coding(SysQualidade, TagIdentidadeIncompleta)
        {
            Display = "Sem CPF — não é possível unir a outras bases",
        });
    }

    private static AdministrativeGender Genero(string? sexo) => (sexo ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "M" => AdministrativeGender.Male,
        "F" => AdministrativeGender.Female,
        _ => AdministrativeGender.Unknown,
    };

    private void Extras(DomainResource r, Dictionary<string, string> extras)
    {
        if (extras.Count == 0) return;
        r.AddExtension(ExtrasUrl, new FhirString(JsonSerializer.Serialize(extras, JsonOpts)));
    }

    // ---------------- Practitioner (canônico: dedup por CPF/conselho; cd_medico prefixado) ----------------

    private static string SiglaConselho(string? v)
    {
        var s = string.IsNullOrWhiteSpace(v) ? "CRM" : v.Trim().ToUpperInvariant();
        return s.Replace("_TE", string.Empty);
    }

    public Practitioner BuildPractitioner(MedicoLinha m)
    {
        var cpf = Dig(m.Cpf);
        var uf = string.IsNullOrWhiteSpace(m.Uf) ? "RJ" : m.Uf.Trim().ToUpperInvariant();
        // Mesma régua canônica do Klinikos: só dígitos, e nada com menos de 3 — senão o
        // UnirIdentifiers derrubaria no merge o que este mapper acabou de emitir.
        var registro = ConselhoPep.Normalizar(m.Crm) ?? string.Empty;
        var sigla = SiglaConselho(m.Conselho);
        var sysConselho = SysConselho + sigla.ToLowerInvariant() + ":" + uf;

        var ident = new List<Identifier>();
        // Só CPF VÁLIDO vira identifier de CPF — repdigit/dígito errado é preenchimento de
        // campo obrigatório, não chave nacional (mesma régua do conector Klinikos).
        if (CpfPep.Valido(cpf)) ident.Add(new Identifier(SysCpf, cpf));                 // nacional
        if (Dig(m.Cns).Length > 0) ident.Add(new Identifier(SysCns, Dig(m.Cns)));   // nacional
        if (registro.Length > 0) ident.Add(new Identifier(sysConselho, registro));  // nacional (conselho)
        if (S(m.Rg) is { } rg)
        {
            var idRg = new Identifier(SysRg, rg);
            if (S(m.Orgao) is { } org) idRg.Assigner = new ResourceReference { Display = org };
            ident.Add(idRg);
        }
        ident.Add(new Identifier(SysSaluxMed, Pref(m.Cd.ToString(CultureInfo.InvariantCulture)))); // interno, por base

        var p = new Practitioner
        {
            Meta = Meta(),
            Identifier = SemVazios(ident),
            Name = [new HumanName { Use = HumanName.NameUse.Official, Text = S(m.Nome) }],
            Active = Verdadeiro(m.Ativo),
            Gender = Genero(m.Sexo),
        };
        if (S(m.Nasc) is { } nasc) p.BirthDate = nasc;
        if (S(m.Email) is { } email) p.Telecom = [new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = email }];
        if (registro.Length > 0)
        {
            p.Qualification =
            [
                new Practitioner.QualificationComponent
                {
                    Identifier = [new Identifier(sysConselho, registro)],
                    Issuer = new ResourceReference { Display = $"{sigla}-{uf}" },
                    Code = new CodeableConcept { Text = S(m.Especialidade) ?? sigla },
                },
            ];
        }

        var extras = new Dictionary<string, string>();
        foreach (var (k, v) in new[] { ("mae", m.Mae), ("pai", m.Pai), ("orgao", m.Orgao),
                                       ("categoria", m.Categoria), ("cbo", m.Cbo), ("especialidade", m.Especialidade) })
        {
            if (S(v) is { } val && val is not "0") extras[k] = val;
        }
        Extras(p, extras);
        return p;
    }

    // ---------------- Patient (canônico: dedup por CPF; cd_paciente prefixado) ----------------

    private Patient.ContactComponent Contato(string relCode, string nome, string? fone = null)
    {
        var c = new Patient.ContactComponent
        {
            Relationship = [new CodeableConcept { Coding = [new Coding { System = SysV3Role, Code = relCode }] }],
            Name = new HumanName { Text = nome },
        };
        if (!string.IsNullOrWhiteSpace(fone))
            c.Telecom = [new ContactPoint { System = ContactPoint.ContactPointSystem.Phone, Value = fone }];
        return c;
    }

    /// <summary>
    /// Unidade de saúde → <c>Organization</c> (ADR-0039). O CNES entra como identifier NACIONAL
    /// — é ele que une a mesma unidade entre PEPs, porque o nome diverge: a mesma UPA é
    /// "UPA 24H INOÃ" no Salux e "UPA MARICA" no Klinikos. O código interno entra prefixado
    /// pelo slug, para o conector reencontrar a linha que ele mesmo criou.
    /// </summary>
    public Organization BuildOrganization(HospitalLinha h)
    {
        var ident = new List<Identifier>();
        if (Dig(h.Cnes) is { Length: > 0 } cnes) ident.Add(new Identifier(SysCnes, cnes));
        ident.Add(new Identifier(SysHospital, Pref(h.Chave)));

        return new Organization
        {
            Meta = Meta(),
            Identifier = SemVazios(ident),
            Name = S(h.Nome),
            Active = h.Ativo is null || Verdadeiro(h.Ativo),
        };
    }

    public Patient BuildPatient(PacienteLinha p)
    {
        var cpf = Dig(p.Cpf);

        // CPF é a chave de dedup, mas NÃO é obrigatório: desde 03/08 o paciente sem CPF entra
        // marcado (recém-nascido, indigente, urgência sem documento). Emitir o identifier com
        // valor vazio faz o hub rejeitar o recurso inteiro com 400 — "'' is not a correct
        // literal for a string. At Patient.identifier[0].value" —, que foi exatamente o que
        // aconteceu nos primeiros ciclos após aquele deploy.
        var ident = new List<Identifier>();
        if (cpf.Length > 0) ident.Add(new Identifier(SysCpf, cpf));
        if (Dig(p.Cns).Length > 0) ident.Add(new Identifier(SysCns, Dig(p.Cns)));
        if (S(p.Rg) is { } rg)
        {
            var idRg = new Identifier(SysRg, rg);
            if (S(p.Orgao) is { } org) idRg.Assigner = new ResourceReference { Display = org };
            ident.Add(idRg);
        }
        foreach (var (val, sys) in new[] { (p.Pis, SysPis), (p.Passaporte, SysPass), (p.Rne, SysRne),
                                           (p.Certidao, SysCert), (p.Sgh, SysSgh), (p.Cem, SysCem) })
        {
            if (S(val) is { } v && v is not "0" and not "None") ident.Add(new Identifier(sys, v));
        }
        ident.Add(new Identifier(SysSaluxPac, Pref(p.Cd.ToString(CultureInfo.InvariantCulture)))); // interno, por base

        var nomes = new List<HumanName> { new() { Use = HumanName.NameUse.Official, Text = S(p.Nome) } };
        if (S(p.Social) is { } social && Verdadeiro(p.FlagSocial))
            nomes.Add(new HumanName { Use = HumanName.NameUse.Nickname, Text = social });

        var pat = new Patient
        {
            Meta = Meta(),
            Identifier = ident,
            Name = nomes,
            Active = Verdadeiro(p.Ativo),
            Gender = Genero(p.Sexo),
        };
        if (S(p.Nasc) is { } nasc) pat.BirthDate = nasc;
        if (S(p.Obito) is { } obito) pat.Deceased = new FhirDateTime(obito);

        var linha = string.Join(" ", new[] { S(p.Logr), S(p.NrLogr) }.Where(x => x is not null));
        var end = new Address();
        var temEnd = false;
        if (!string.IsNullOrWhiteSpace(linha))
        {
            var linhas = new List<string> { linha };
            if (S(p.Compl) is { } compl) linhas.Add(compl);
            end.Line = linhas;
            temEnd = true;
        }
        if (S(p.Bairro) is { } bairro) { end.District = bairro; temEnd = true; }
        if (Dig(p.Cep).Length > 0) { end.PostalCode = Dig(p.Cep); temEnd = true; }
        if (S(p.Ref) is { } refe) { end.Text = "Ref: " + refe; temEnd = true; }
        if (S(p.Cidade) is { } cidade) { end.City = cidade; temEnd = true; }
        if (S(p.UfSigla) is { } ufs) { end.State = UfIbge.GetValueOrDefault(ufs, ufs); temEnd = true; }
        if (temEnd) { end.Use = Address.AddressUse.Home; pat.Address = [end]; }

        if (S(p.EstadoCivilDs) is { } ec) pat.MaritalStatus = new CodeableConcept { Text = ec };

        var tel = new List<ContactPoint>();
        var fone = !string.IsNullOrWhiteSpace(p.Fone) ? Dig(p.Ddd) + Dig(p.Fone) : string.Empty;
        if (fone.Length > 0) tel.Add(new ContactPoint { System = ContactPoint.ContactPointSystem.Phone, Value = fone, Use = ContactPoint.ContactPointUse.Home });
        if (S(p.Email) is { } email) tel.Add(new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = email });
        if (tel.Count > 0) pat.Telecom = tel;

        var contatos = new List<Patient.ContactComponent>();
        if (S(p.Mae) is { } mae) contatos.Add(Contato("MTH", mae));
        if (S(p.Pai) is { } pai) contatos.Add(Contato("FTH", pai));
        if (S(p.Conjuge) is { } conj) contatos.Add(Contato("SPS", conj));
        if (S(p.Responsavel) is { } resp)
        {
            var foneResp = !string.IsNullOrWhiteSpace(p.FoneResp) ? Dig(p.DddResp) + Dig(p.FoneResp) : null;
            contatos.Add(Contato("GUARD", resp, foneResp));
        }
        if (contatos.Count > 0) pat.Contact = contatos;

        var extras = new Dictionary<string, string>();
        foreach (var (k, v) in new[] {
            ("cd_cor", p.CdCor), ("cd_nacionalidade", p.CdNacionalidade), ("pais", p.Pais),
            ("profissao", p.Profissao), ("ocupacao", p.Ocupacao), ("peso", p.Peso), ("altura", p.Altura),
            ("sangue", p.Sangue), ("rh", p.Rh), ("etnia", p.Etnia), ("grau_parentesco", p.GrauParentesco),
            ("entrada_pais", p.EntradaPais) })
        {
            if (S(v) is { } val && val is not "0") extras[k] = val;
        }
        if (S(p.EstadoCivilDs) is { } ecd) extras["estado_civil"] = ecd;
        if (S(p.InstrucaoDs) is { } ins) extras["escolaridade"] = ins;
        if (S(p.ReligiaoDs) is { } rel) extras["religiao"] = rel;
        if (S(p.BarreiraDs) is { } bar) extras["barreira_comunicacao"] = bar;
        Extras(pat, extras);

        return pat;
    }

    // ---------------- Atendimentos (Encounter/DocRef prefixados por base) ----------------

    public Encounter BuildEncounter(BaaLinha b, string patientRef, string? organizationRef = null)
    {
        var emerg = (b.Emerg ?? string.Empty).ToUpperInvariant() == "S";
        var start = Dt(b.DtCheg) ?? Dt(b.DtAtend);
        var enc = new Encounter
        {
            Meta = Meta(),
            Status = Encounter.EncounterStatus.Finished,
            Class = new Coding
            {
                System = SysClass,
                Code = emerg ? "EMER" : "AMB",
                Display = emerg ? "emergency" : "ambulatory",
            },
            Subject = new ResourceReference(patientRef),
            Identifier = [new Identifier(SysBaa, Pref(b.Chave))],
        };
        // ONDE aconteceu (ADR-0039). Dimensão ortogonal ao meta.source, que diz de qual SISTEMA
        // veio: a unidade sobrevive à troca de PEP, o sistema não.
        if (organizationRef is not null) enc.ServiceProvider = new ResourceReference(organizationRef);
        if (start is not null)
        {
            enc.Period = new Period { Start = start };
            if (Dt(b.DtSaida) is { } fim) enc.Period.End = fim;
        }
        if (S(b.Medico) is { } medico)
            enc.Participant = [new Encounter.ParticipantComponent { Individual = new ResourceReference { Display = medico } }];
        return enc;
    }

    public Condition? BuildCondition(BaaLinha b, string patientRef, string encRef)
    {
        var cid = S(b.Cid);
        if (cid is null) return null;
        var ds = S(b.CidDs);

        // CID-10 cruz-estrela: a origem grava o marcador (†/*) junto do código ("L14 *"),
        // mas o tipo `code` do FHIR não aceita espaço/asterisco. Usamos só a base ("L14")
        // no `code` e preservamos o original no `text` (repositório fiel).
        var codigo = LimparCodigoCid(cid);
        var code = new CodeableConcept { Text = ds ?? cid };
        if (codigo is not null)
            code.Coding = [new Coding { System = SysCid, Code = codigo, Display = ds }];

        return new Condition
        {
            Meta = Meta(),
            Subject = new ResourceReference(patientRef),
            Encounter = new ResourceReference(encRef),
            Code = code,
            // Identifier determinístico (ADR-0024): 1 Condition por BAA → reimport atualiza, não duplica.
            Identifier = [new Identifier(SysBaa, Pref(b.Chave) + ":cond")],
        };
    }

    // ---------------- Internação: FIA → Encounter IMP + Location (ADR-0025) ----------------

    /// <summary>Janela além da qual FIA sem alta é "zumbi" (mesma régua do painel).</summary>
    public static readonly TimeSpan JanelaEmCurso = TimeSpan.FromDays(120);

    /// <summary>
    /// Encounter class=IMP a partir da FIA. Diagnóstico vai como Condition separada (mesmo
    /// padrão do BAA — o consumidor liga por Condition.encounter, não por Encounter.diagnosis).
    /// Óbito hospitalar vira dischargeDisposition=exp; o óbito do PACIENTE continua fluindo
    /// só por paciente.dt_obito no fluxo canônico (duas fontes = conflito).
    /// </summary>
    public Encounter BuildEncounterInternacao(FiaLinha f, string patientRef, string? leitoRef, FiaLeitoLinha? leitoAtual, DateTime agoraUtc, string? organizationRef = null)
    {
        // status R4 (não existe "discharged" em R4): em curso / finalizada / zumbi.
        var baixaUtc = Leitura.SaluxTempo.ParseUtc(f.DtBaixa);
        var status = f.DtAlta is not null ? Encounter.EncounterStatus.Finished
            : baixaUtc is { } b && agoraUtc - b <= JanelaEmCurso ? Encounter.EncounterStatus.InProgress
            : Encounter.EncounterStatus.Unknown;

        var enc = new Encounter
        {
            Meta = Meta(),
            Status = status,
            Class = new Coding { System = SysClass, Code = "IMP", Display = "inpatient encounter" },
            Subject = new ResourceReference(patientRef),
            Identifier = [new Identifier(SysFia, Pref(f.Chave))],
        };
        // Internação também carrega a unidade: é o dado que responde "onde a pessoa esteve
        // internada" mesmo depois de o PEP de origem ser desligado (ADR-0039).
        if (organizationRef is not null) enc.ServiceProvider = new ResourceReference(organizationRef);

        if (Dt(f.DtBaixa) is { } inicio)
        {
            enc.Period = new Period { Start = inicio };
            if (Dt(f.DtAlta) is { } fim) enc.Period.End = fim;
        }

        // Caráter SUS → v3-ActPriority. FIA.ID_INTERNACAO ('U'/'E') NÃO é usado — no HMCML é
        // convenção de recepção, não urgência/eletiva (fato documentado no painel/ADR-0025).
        if (PriorityDoCarater(f.Carater) is { } prio)
            enc.Priority = new CodeableConcept(SysPriority, prio.Codigo, prio.Display, null);

        // Óbito hospitalar. Alta comum NÃO recebe código — a FIA não tem motivo de alta
        // levantado; inventar "home" seria fabricar dado.
        if (S(f.NrObito) is not null)
            enc.Hospitalization = new Encounter.HospitalizationComponent
            {
                DischargeDisposition = new CodeableConcept(SysDischarge, "exp", "Expired", null),
            };

        if (leitoRef is not null)
        {
            var loc = new Encounter.LocationComponent
            {
                Location = new ResourceReference(leitoRef),
                Status = status == Encounter.EncounterStatus.InProgress && leitoAtual?.DtSaidaLeito is null
                    ? Encounter.EncounterLocationStatus.Active
                    : Encounter.EncounterLocationStatus.Completed,
            };
            if (Dt(leitoAtual?.DtTransferencia) is { } t)
            {
                loc.Period = new Period { Start = t };
                if (Dt(leitoAtual?.DtSaidaLeito) is { } sfim) loc.Period.End = sfim;
            }
            enc.Location = [loc];
        }

        var extras = new Dictionary<string, string>();
        if (S(f.DtPrevisaoAlta) is { } prev) extras["dt_previsao_alta"] = prev;
        if (S(f.DtAltaMedica) is { } am) extras["dt_alta_medica"] = am;
        if (S(f.Carater) is { } car) extras["carater_internacao"] = car;
        if (S(f.CaraterDs) is { } cds) extras["carater_internacao_ds"] = cds;
        Extras(enc, extras);
        return enc;
    }

    /// <summary>Caráter de internação SUS → v3-ActPriority (EL eletivo / UR urgência). Null = não mapeável.</summary>
    private static (string Codigo, string Display)? PriorityDoCarater(string? carater) => S(carater) switch
    {
        "1" or "11" => ("EL", "elective"),
        "2" or "20" or "21" or "26" or "27" or "28" or "29" => ("UR", "urgent"),
        _ => null,
    };

    public Condition? BuildConditionFia(FiaLinha f, string patientRef, string encRef)
    {
        var cid = S(f.Cid);
        if (cid is null) return null;
        var ds = S(f.CidDs);

        var codigo = LimparCodigoCid(cid);
        var code = new CodeableConcept { Text = ds ?? cid };
        if (codigo is not null)
            code.Coding = [new Coding { System = SysCid, Code = codigo, Display = ds }];

        var cond = new Condition
        {
            Meta = Meta(),
            Subject = new ResourceReference(patientRef),
            Encounter = new ResourceReference(encRef),
            Code = code,
            Identifier = [new Identifier(SysFia, Pref(f.Chave) + ":cond")],
        };
        return cond;
    }

    public Location BuildLocationSetor(UnidadeLinha u) => LocationBase(
        SysUnidade, Pref(u.Chave), S(u.Nome) ?? $"Setor {u.Cd}", "wa", "Ward",
        (u.Condicao ?? "A").Trim().ToUpperInvariant() == "A" ? Location.LocationStatus.Active : Location.LocationStatus.Inactive,
        partOfRef: null);

    public Location BuildLocationQuarto(QuartoLinha q, string? setorRef)
    {
        var loc = LocationBase(SysQuarto, Pref(q.Chave), $"Quarto {S(q.CdQuarto)}", "ro", "Room",
            Location.LocationStatus.Active, setorRef);
        var extras = new Dictionary<string, string>();
        if (S(q.Isolamento) is { } iso) extras["in_isolamento"] = iso;
        if (S(q.Sexo) is { } sx) extras["sexo"] = sx;
        Extras(loc, extras);
        return loc;
    }

    public Location BuildLocationLeito(LeitoLinha l, string? quartoRef)
    {
        // Bloqueado ('F') é estado semi-duradouro → suspended; desativado → inactive.
        // Ocupado/livre é foto operacional e NÃO entra no hub (ADR-0025).
        var status = (l.IdCondicao ?? "A").Trim().ToUpperInvariant() == "I" ? Location.LocationStatus.Inactive
            : (l.IdSitLeito ?? string.Empty).Trim().ToUpperInvariant() == "F" ? Location.LocationStatus.Suspended
            : Location.LocationStatus.Active;
        var loc = LocationBase(SysLeito, Pref(l.Chave), $"Leito {S(l.CdLeito)}", "bd", "Bed", status, quartoRef);
        var extras = new Dictionary<string, string>();
        if (S(l.IdLeito) is { } tipo) extras["id_leito"] = tipo; // I/E/O/C/R/V
        Extras(loc, extras);
        return loc;
    }

    private Location LocationBase(string system, string valor, string nome, string physCode, string physDisplay,
        Location.LocationStatus status, string? partOfRef)
    {
        var loc = new Location
        {
            Meta = Meta(),
            Identifier = [new Identifier(system, valor)],
            Name = nome,
            Status = status,
            Mode = Location.LocationMode.Instance,
            PhysicalType = new CodeableConcept(SysLocPhysType, physCode, physDisplay, null),
        };
        if (partOfRef is not null) loc.PartOf = new ResourceReference(partOfRef);
        return loc;
    }

    /// <summary>
    /// Reduz um CID-10 ao literal válido de `code` do FHIR: corta no primeiro espaço ou
    /// marcador cruz-estrela (†/‡/*/+). Ex.: "L14 *" → "L14", "A09.0" → "A09.0".
    /// Devolve null se nada sobrar.
    /// </summary>
    private static string? LimparCodigoCid(string codigo)
    {
        var t = codigo.Trim();
        var fim = t.Length;
        for (var i = 0; i < t.Length; i++)
        {
            var ch = t[i];
            if (char.IsWhiteSpace(ch) || ch is '*' or '+' or '†' or '‡')
            {
                fim = i;
                break;
            }
        }
        var limpo = t[..fim];
        return limpo.Length > 0 ? limpo : null;
    }

    private static string? TextoPosologia(PrescricaoLinha item)
    {
        var partes = new List<string>();
        if (S(item.Qt) is { } qt) partes.Add($"Qtd: {qt}");
        if (S(item.Horario) is { } h) partes.Add(h);
        if (S(item.Obs) is { } o) partes.Add(o);
        return partes.Count > 0 ? string.Join(" — ", partes) : null;
    }

    public MedicationRequest BuildMedicationRequest(PrescricaoLinha item, string patientRef, string encRef, string? authoredOn)
    {
        var mat = S(item.Mat) ?? $"Material {item.CdMat}";
        var mr = new MedicationRequest
        {
            Meta = Meta(),
            // Identifier determinístico por ITEM de prescrição (nr_prescricao + seq_item).
            Identifier = [new Identifier(SysPresc, Pref($"{item.ChaveBaa}-{item.NrPrescricao}-{item.SeqItem}"))],
            Status = MedicationRequest.MedicationrequestStatus.Completed,
            Intent = MedicationRequest.MedicationRequestIntent.Order,
            Medication = new CodeableConcept
            {
                Coding = [new Coding { System = SysMatmed, Code = item.CdMat, Display = mat }],
                Text = mat,
            },
            Subject = new ResourceReference(patientRef),
            Encounter = new ResourceReference(encRef),
        };
        if (S(authoredOn) is { } a) mr.AuthoredOn = a;
        if ((item.Urg ?? string.Empty).ToUpperInvariant() == "S") mr.Priority = RequestPriority.Urgent;
        if (S(item.Medico) is { } medico) mr.Requester = new ResourceReference { Display = medico };
        if (TextoPosologia(item) is { } pos) mr.DosageInstruction = [new Dosage { Text = pos }];
        return mr;
    }

    public DocumentReference BuildDocRef(EdocLinha doc, string html, string patientRef, string encRef)
    {
        var titulo = S(doc.Modelo) ?? "Documento";
        var d = new DocumentReference
        {
            Meta = Meta(),
            Status = DocumentReferenceStatus.Current,
            Type = new CodeableConcept { Text = titulo },
            Subject = new ResourceReference(patientRef),
            Identifier = [new Identifier(SysEdoc, Pref(doc.ChaveDoc))],
            Content =
            [
                new DocumentReference.ContentComponent
                {
                    Attachment = new Attachment
                    {
                        ContentType = "text/html",
                        Data = Encoding.UTF8.GetBytes(html),
                        Title = titulo,
                    },
                },
            ],
            Context = new DocumentReference.ContextComponent { Encounter = [new ResourceReference(encRef)] },
        };
        if (Dt(doc.Dt) is { } data && DateTimeOffset.TryParse(data, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto))
            d.Date = dto;
        return d;
    }

    // ---------------- Observations ----------------

    private Observation Esqueleto(string patientRef, string encRef, string? effective, string categoria)
    {
        var o = new Observation
        {
            Meta = Meta(),
            Status = ObservationStatus.Final,
            Category = [new CodeableConcept { Coding = [new Coding { System = SysObsCat, Code = categoria }] }],
            Subject = new ResourceReference(patientRef),
            Encounter = new ResourceReference(encRef),
        };
        if (effective is not null) o.Effective = new FhirDateTime(effective);
        return o;
    }

    private Observation BuildObsQuantity(string patientRef, string encRef, string? eff,
        string loinc, string display, decimal valor, string unidade, string ucum)
    {
        var o = Esqueleto(patientRef, encRef, eff, "vital-signs");
        o.Code = new CodeableConcept { Coding = [new Coding { System = SysLoinc, Code = loinc, Display = display }], Text = display };
        o.Value = new Quantity { Value = valor, Unit = unidade, System = SysUcum, Code = ucum };
        return o;
    }

    private Observation BuildObsPressao(string patientRef, string encRef, string? eff, decimal? sist, decimal? diast)
    {
        var o = Esqueleto(patientRef, encRef, eff, "vital-signs");
        o.Code = new CodeableConcept { Coding = [new Coding { System = SysLoinc, Code = "85354-9", Display = "Pressão arterial" }], Text = "Pressão arterial" };
        var comps = new List<Observation.ComponentComponent>();
        if (sist is not null)
            comps.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept { Coding = [new Coding { System = SysLoinc, Code = "8480-6", Display = "Pressão sistólica" }] },
                Value = new Quantity { Value = sist, Unit = "mmHg", System = SysUcum, Code = "mm[Hg]" },
            });
        if (diast is not null)
            comps.Add(new Observation.ComponentComponent
            {
                Code = new CodeableConcept { Coding = [new Coding { System = SysLoinc, Code = "8462-4", Display = "Pressão diastólica" }] },
                Value = new Quantity { Value = diast, Unit = "mmHg", System = SysUcum, Code = "mm[Hg]" },
            });
        o.Component = comps;
        return o;
    }

    public Observation BuildObsRisco(string chaveBaa, string patientRef, string encRef, string? eff, string cor)
    {
        var o = Esqueleto(patientRef, encRef, eff, "survey");
        o.Identifier = [new Identifier(SysBaa, Pref(chaveBaa) + ":risco")];
        o.Code = new CodeableConcept
        {
            Coding = [new Coding { System = SysRisco, Code = "classificacao-risco", Display = "Classificação de risco" }],
            Text = "Classificação de risco",
        };
        o.Value = new CodeableConcept { Coding = [new Coding { System = SysRisco, Display = cor }], Text = cor };
        return o;
    }

    private static readonly (string Chave, string Tipo)[] MatchersVital =
    [
        ("pressão arterial", "pa"), ("pulso", "fc"), ("frequência cardíaca", "fc"), ("freq. cardíaca", "fc"),
        ("frequência respiratória", "fr"), ("sat o2", "spo2"), ("saturação", "spo2"), ("temperatura", "temp"),
    ];

    private static string? ClassificarVital(string? label)
    {
        var l = (label ?? string.Empty).ToLowerInvariant();
        foreach (var (chave, tipo) in MatchersVital)
            if (l.Contains(chave)) return tipo;
        return null;
    }

    public List<Observation> ObservationsDeEdoc(string chaveDoc, IReadOnlyList<EdocItemLinha> itens, string patientRef, string encRef, string? effective)
    {
        var porTipo = new Dictionary<string, string>();
        foreach (var it in itens.OrderBy(x => (x.Label ?? string.Empty).ToLowerInvariant().Contains("acolhimento") ? 0 : 1))
        {
            var tipo = ClassificarVital(it.Label);
            var resp = S(it.Resp);
            if (tipo is not null && resp is not null && !porTipo.ContainsKey(tipo))
                porTipo[tipo] = resp;
        }

        var obs = new List<Observation>();
        // Identifier determinístico: 1 Observation por TIPO de vital por documento.
        void Ident(Observation o, string tipo) => o.Identifier = [new Identifier(SysEdoc, Pref(chaveDoc) + ":" + tipo)];

        if (porTipo.TryGetValue("pa", out var pa))
        {
            var (sist, diast) = ParsePa(pa);
            if (sist is not null || diast is not null)
            {
                var o = BuildObsPressao(patientRef, encRef, effective, sist, diast);
                Ident(o, "pa");
                obs.Add(o);
            }
        }
        void Add(string tipo, string loinc, string display, decimal valor, string unidade, string ucum)
        {
            var o = BuildObsQuantity(patientRef, encRef, effective, loinc, display, valor, unidade, ucum);
            Ident(o, tipo);
            obs.Add(o);
        }
        if (porTipo.TryGetValue("fc", out var fc) && Num(fc) is { } vfc)
            Add("fc", "8867-4", "Frequência cardíaca", vfc, "bpm", "/min");
        if (porTipo.TryGetValue("fr", out var fr) && Num(fr) is { } vfr)
            Add("fr", "9279-1", "Frequência respiratória", vfr, "irpm", "/min");
        if (porTipo.TryGetValue("temp", out var tp) && Num(tp) is { } vtp)
            Add("temp", "8310-5", "Temperatura", vtp, "°C", "Cel");
        if (porTipo.TryGetValue("spo2", out var sp) && Num(sp) is { } vsp)
            Add("spo2", "2708-6", "Saturação de O₂", vsp, "%", "%");
        return obs;
    }

    public static string MontarHtml(string? modelo, IReadOnlyList<EdocItemLinha> itens)
    {
        var grupos = new List<(string Rotulo, List<string> Valores)>();
        foreach (var it in itens)
        {
            var resp = S(it.Resp);
            if (resp is null) continue;
            var label = S(it.Label) ?? string.Empty;
            if (grupos.Count > 0 && grupos[^1].Rotulo == label)
                grupos[^1].Valores.Add(resp);
            else
                grupos.Add((label, [resp]));
        }

        var campos = new StringBuilder();
        foreach (var (label, valores) in grupos)
        {
            var valor = string.Concat(valores.Select(v => $"<div class=\"edoc-linha\">{Escape(v)}</div>"));
            var rotulo = label.Length > 0 ? $"<span class=\"edoc-rotulo\">{Escape(label)}</span>" : string.Empty;
            campos.Append($"<div class=\"edoc-campo\">{rotulo}<div class=\"edoc-valor\">{valor}</div></div>");
        }

        var corpo = campos.Length > 0 ? campos.ToString() : "<p class=\"edoc-vazio\">(documento sem itens preenchidos)</p>";
        return $"<section class=\"edoc-doc\"><h3 class=\"edoc-titulo\">{Escape(modelo ?? "Documento")}</h3>{corpo}</section>";
    }

    private static string Escape(string v) => HtmlEncoder.Default.Encode(v);
}
