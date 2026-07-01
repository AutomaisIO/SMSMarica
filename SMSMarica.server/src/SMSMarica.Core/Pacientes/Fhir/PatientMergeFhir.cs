using Hl7.Fhir.Model;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Pacientes.Fhir;

/// <summary>
/// Motor de MERGE de demografia sobre um <see cref="Patient"/> FHIR nativo. Fonte única
/// da semântica de escrita nativa — reutilizada pela edição (PacienteFhirMapper), pela
/// importação (P0.3) e pelo backfill (R4). Espelha as formas do <c>SaluxFhirMapper.BuildPatient</c>
/// para que paciente criado pelo painel e paciente importado fiquem idênticos no hub.
///
/// Regra de ouro: <b>upsert, nunca replace-all</b>. Valor vazio é no-op (não apaga o
/// existente), para não destruir dados de outra origem (ADR-0020).
/// </summary>
public static class PatientMergeFhir
{
    public const string SystemCpf = "https://fhir.saude.gov.br/sid/cpf";
    public const string SystemCns = "https://fhir.saude.gov.br/sid/cns";
    public const string SystemRg = "urn:br:gov:rg";

    private const string SysV3Role = "http://terminology.hl7.org/CodeSystem/v3-RoleCode";
    private const string SysV3Marital = "http://terminology.hl7.org/CodeSystem/v3-MaritalStatus";
    private const string ExtHouseNumber = "http://hl7.org/fhir/StructureDefinition/iso21090-ADXP-houseNumber";

    // ---------------- Nome ----------------

    /// <summary>Upsert do nome oficial. Vazio preserva o existente (nunca emite text vazio → 400 no hub).</summary>
    public static void UpsertNomeOficial(Patient p, string? texto)
    {
        var limpo = texto?.Trim();
        if (string.IsNullOrWhiteSpace(limpo)) return;
        p.Name ??= [];
        var oficial = p.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official);
        if (oficial is null)
        {
            oficial = new HumanName { Use = HumanName.NameUse.Official };
            p.Name.Insert(0, oficial);
        }
        oficial.Text = limpo;
        oficial.Family = null;
        oficial.GivenElement = [];
    }

    /// <summary>Upsert do nome social (HumanName use=nickname). Vazio remove o apelido gerido.</summary>
    public static void UpsertNomeSocial(Patient p, string? texto)
    {
        var limpo = texto?.Trim();
        p.Name ??= [];
        var apelido = p.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Nickname);
        if (string.IsNullOrWhiteSpace(limpo))
        {
            if (apelido is not null) p.Name.Remove(apelido);
            return;
        }
        if (apelido is null) p.Name.Add(new HumanName { Use = HumanName.NameUse.Nickname, Text = limpo });
        else apelido.Text = limpo;
    }

    // ---------------- Identifiers ----------------

    /// <summary>Upsert por <c>system</c>, preservando os demais (PIS, cd_paciente, SGH/CEM…) e o Assigner existente.</summary>
    public static void UpsertIdentifier(Patient p, string system, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return;
        p.Identifier ??= [];
        var existente = p.Identifier.FirstOrDefault(i => i.System == system);
        if (existente is null) p.Identifier.Add(new Identifier(system, valor.Trim()));
        else existente.Value = valor.Trim();
    }

    // ---------------- Demografia central ----------------

    public static void SetBirthDate(Patient p, DateOnly? data)
    {
        if (data is { } d) p.BirthDate = d.ToString("yyyy-MM-dd");
    }

    public static void SetGender(Patient p, Sexo sexo) =>
        // Decisão do produto: gender SOBRESCREVE mesmo com NaoInformado (→ Unknown).
        // Diferente de SetMaritalStatus, que preserva. O form sempre reenvia o sexo
        // (ParaDto lê nativo), então round-trip; NaoInformado explícito zera para Unknown.
        p.Gender = sexo switch
        {
            Sexo.Masculino => AdministrativeGender.Male,
            Sexo.Feminino => AdministrativeGender.Female,
            _ => AdministrativeGender.Unknown,
        };

    public static void SetMaritalStatus(Patient p, EstadoCivil ec)
    {
        var code = ec switch
        {
            EstadoCivil.Solteiro => "S",
            EstadoCivil.Casado => "M",
            EstadoCivil.UniaoEstavel => "T",
            EstadoCivil.Divorciado => "D",
            EstadoCivil.Viuvo => "W",
            EstadoCivil.Separado => "L",
            _ => null,
        };
        if (code is null) return; // NaoInformado: preserva o que houver (ex.: text do importado)
        p.MaritalStatus = new CodeableConcept
        {
            Coding = [new Coding { System = SysV3Marital, Code = code }],
            Text = RotuloEstadoCivil(ec),
        };
    }

    private static string RotuloEstadoCivil(EstadoCivil ec) => ec switch
    {
        EstadoCivil.Solteiro => "Solteiro(a)",
        EstadoCivil.Casado => "Casado(a)",
        EstadoCivil.UniaoEstavel => "União estável",
        EstadoCivil.Divorciado => "Divorciado(a)",
        EstadoCivil.Viuvo => "Viúvo(a)",
        EstadoCivil.Separado => "Separado(a)",
        _ => "Não informado",
    };

    // ---------------- Endereço ----------------

    /// <summary>
    /// Upsert do endereço residencial (address[0], use=home). Null/vazio preserva o existente.
    /// O número vai numa extension iso21090-ADXP-houseNumber no Line[0] (round-trip do campo Numero).
    /// </summary>
    public static void UpsertEndereco(Patient p, EnderecoDto? e)
    {
        if (e is null) return;
        var logradouro = (e.Logradouro ?? string.Empty).Trim();
        var numero = e.Numero?.Trim();
        var complemento = e.Complemento?.Trim();
        var cep = Digitos(e.Cep);
        var bairro = (e.Bairro ?? string.Empty).Trim();
        var cidade = (e.Cidade ?? string.Empty).Trim();
        var uf = (e.Uf ?? string.Empty).Trim().ToUpperInvariant();
        var refe = e.PontoReferencia?.Trim();

        if (logradouro.Length == 0 && cidade.Length == 0 && cep.Length == 0) return;

        var linhas = new List<string>();
        var line0 = new FhirString(logradouro);
        if (!string.IsNullOrWhiteSpace(numero))
            line0.AddExtension(ExtHouseNumber, new FhirString(numero));
        linhas.Add(logradouro);
        if (!string.IsNullOrWhiteSpace(complemento)) linhas.Add(complemento);

        var end = new Address
        {
            Use = Address.AddressUse.Home,
            LineElement = string.IsNullOrWhiteSpace(complemento)
                ? [line0]
                : [line0, new FhirString(complemento)],
        };
        if (bairro.Length > 0) end.District = bairro;
        if (cep.Length > 0) end.PostalCode = cep;
        if (cidade.Length > 0) end.City = cidade;
        if (uf.Length > 0) end.State = uf;
        if (!string.IsNullOrWhiteSpace(refe)) end.Text = refe;

        // Substitui o endereço residencial gerido; preserva outros usos (work/temp), se houver.
        p.Address ??= [];
        var outros = p.Address.Where(a => a.Use != Address.AddressUse.Home).ToList();
        p.Address = [end, .. outros];
    }

    // ---------------- Telecom ----------------

    /// <summary>
    /// Aplica os telefones/e-mail geridos pelo painel. Modelo: principal = phone rank=1;
    /// celular = phone use=mobile; residencial = phone use=home; e-mail = email.
    /// Dedupe por dígitos; preserva telecoms não geridos. <b>Vazio é no-op</b> (não remove) —
    /// preservação-first (ADR-0020): evita apagar o telefone nativo do importado quando o
    /// slot vem vazio no DTO. Limpar um telefone não é suportado nesta fase.
    /// </summary>
    private static readonly IReadOnlySet<string> SemProtecao = new HashSet<string>();

    /// <param name="protegidos">
    /// Dígitos de telefones CONFIRMADOS (validados por OTP/WhatsApp, <c>contato_validado</c>).
    /// Invariante (ADR-0020): automação NUNCA toca um número confirmado — nem altera, nem
    /// remove, nem reordena, nem substitui. O chamador (edição/import/backfill) deve informá-los.
    /// </param>
    public static void AplicarContatos(Patient p, string? principal, string? celular, string? residencial,
        string? email, IReadOnlySet<string>? protegidos = null)
    {
        p.Telecom ??= [];
        var prot = protegidos ?? SemProtecao;

        UpsertTelefone(p, principal, use: null, rank: 1, prot);
        UpsertTelefone(p, celular, use: ContactPoint.ContactPointUse.Mobile, rank: null, prot);
        UpsertTelefone(p, residencial, use: ContactPoint.ContactPointUse.Home, rank: null, prot);
        UpsertEmail(p, email);
    }

    private static void UpsertTelefone(Patient p, string? valor, ContactPoint.ContactPointUse? use, int? rank,
        IReadOnlySet<string> protegidos)
    {
        var digitos = Digitos(valor);
        if (digitos.Length == 0) return; // vazio = no-op (nunca remove; preservação-first)
        // Número confirmado é intocável: se o próprio valor é protegido, ele já existe e não
        // deve ser recriado/movido por automação.
        if (protegidos.Contains(digitos)) return;

        bool ehTelefone(ContactPoint t) => t.System == ContactPoint.ContactPointSystem.Phone;
        // Slot gerido: principal identifica-se por Rank==1; os demais por Use (e não-principal).
        var slot = rank == 1
            ? p.Telecom.FirstOrDefault(t => ehTelefone(t) && t.Rank == 1)
            : p.Telecom.FirstOrDefault(t => ehTelefone(t) && t.Use == use && t.Rank != 1);

        // NUNCA tocar num slot cujo número é confirmado (ex.: mudar o principal quando o
        // atual está validado) — o número validado é imutável para a automação.
        if (slot is not null && protegidos.Contains(Digitos(slot.Value))) return;

        // Sem slot gerido: reusa um telefone de mesmos dígitos (ex.: o telefone nativo do
        // importado) em vez de duplicar — nunca colapsa um não-principal no slot principal,
        // nem reusa um número confirmado.
        slot ??= p.Telecom.FirstOrDefault(t =>
            ehTelefone(t) && Digitos(t.Value) == digitos && (rank == 1 || t.Rank != 1)
            && !protegidos.Contains(Digitos(t.Value)));

        if (slot is null)
        {
            slot = new ContactPoint { System = ContactPoint.ContactPointSystem.Phone };
            if (rank != 1) slot.Use = use; // Use só em slot novo não-principal; não muta reusados
            p.Telecom.Add(slot);
        }
        slot.Value = digitos;
        if (rank == 1) slot.Rank = 1;
    }

    private static void UpsertEmail(Patient p, string? email)
    {
        var limpo = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(limpo)) return; // vazio = no-op (preservação-first)
        var slot = p.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email);
        if (slot is null) p.Telecom.Add(new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = limpo });
        else slot.Value = limpo;
    }

    // ---------------- Filiação (Patient.contact) ----------------

    /// <summary>Upsert de um contato de parentesco (MTH/FTH/GUARD/SPS, v3-RoleCode). Vazio remove.</summary>
    public static void UpsertContato(Patient p, string relCode, string? nome, string? fone = null)
    {
        p.Contact ??= [];
        var existente = p.Contact.FirstOrDefault(c =>
            c.Relationship?.Any(r => r.Coding?.Any(cd => cd.System == SysV3Role && cd.Code == relCode) == true) == true);

        var limpo = nome?.Trim();
        if (string.IsNullOrWhiteSpace(limpo))
        {
            if (existente is not null) p.Contact.Remove(existente);
            return;
        }

        if (existente is null)
        {
            existente = new Patient.ContactComponent
            {
                Relationship = [new CodeableConcept { Coding = [new Coding { System = SysV3Role, Code = relCode }] }],
            };
            p.Contact.Add(existente);
        }
        existente.Name = new HumanName { Text = limpo };
        var foneDig = Digitos(fone);
        // Upsert, nunca replace: fone vazio preserva o telecom existente (ex.: fone do
        // responsável gravado pelo import Salux), pois o painel não expõe esse campo.
        if (foneDig.Length > 0)
            existente.Telecom = [new ContactPoint { System = ContactPoint.ContactPointSystem.Phone, Value = foneDig }];
        else if (existente.Telecom is not { Count: > 0 })
            existente.Telecom = null;
    }

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
}
