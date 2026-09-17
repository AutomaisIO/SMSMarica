using Hl7.Fhir.Model;
using SMSMais.Core.Common.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Pacientes.Fhir;

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

    /// <summary>Extension FHIR padrão de geolocalização (lat/long) em Address.</summary>
    public const string ExtGeolocation = "http://hl7.org/fhir/StructureDefinition/geolocation";

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

    /// <summary>Grava a geolocalização (extension FHIR padrão) no endereço residencial. No-op sem endereço.</summary>
    public static void SetGeolocation(Patient p, double latitude, double longitude)
    {
        var a = p.Address?.FirstOrDefault(x => x.Use == Address.AddressUse.Home) ?? p.Address?.FirstOrDefault();
        if (a is null) return; // geolocation pertence a um endereço
        a.RemoveExtension(ExtGeolocation);
        var geo = new Extension { Url = ExtGeolocation };
        geo.AddExtension("latitude", new FhirDecimal((decimal)latitude));
        geo.AddExtension("longitude", new FhirDecimal((decimal)longitude));
        a.Extension.Add(geo);
    }

    // ---------------- Telecom ----------------

    /// <summary>
    /// Aplica os telefones/e-mail geridos pelo painel. Modelo: principal = phone rank=1;
    /// celular = phone use=mobile; residencial = phone use=home; e-mail = email.
    /// Dedupe por dígitos; preserva telecoms não geridos. <b>Vazio é no-op</b> (não remove) —
    /// preservação-first (ADR-0020): evita apagar o telefone nativo do importado quando o
    /// slot vem vazio no DTO. Limpar um telefone não é suportado nesta fase.
    /// </summary>
    /// <summary>
    /// Marcador (extension em ContactPoint) de contato CONFIRMADO por OTP/WhatsApp.
    /// Invariante (ADR-0020, decisão #2): a automação (edição/import/backfill) NUNCA altera,
    /// remove, reordena ou substitui um telecom que carrega este marcador. A fonte é o próprio
    /// Patient — não precisa consultar o banco. Carimbado por <see cref="MarcarTelefoneConfirmado"/>.
    /// </summary>
    public const string ExtContatoConfirmado = "urn:smsmarica:contato-confirmado";

    /// <summary>Marcador de contato NEGADO: quem atendeu este número disse que NÃO é o paciente
    /// ("não sou essa pessoa"). É o par de alerta do confirmado — o ✔ vira ❗ na tela. Uma
    /// verificação positiva posterior (OTP) limpa o marcador.</summary>
    public const string ExtContatoNegado = "urn:smsmarica:contato-negado";

    /// <summary>Marcador (no Patient) das chaves de campo que o PAINEL editou — o import não as sobrescreve
    /// (ADR-0020 decisão #1: "painel vence no que editou"). Chaves: telefone,email,endereco,nomeSocial,estadoCivil,filiacao.</summary>
    public const string ExtCamposEditados = "urn:smsmarica:campos-editados";

    /// <summary>Blob proprietário do smsmarica (payload JSON) — preservado no reimport (Oracle não tem).</summary>
    public const string ExtPayloadBlob = "urn:smsmarica:paciente-payload";

    /// <param name="manual">
    /// true = edição humana pelo painel: trocar o principal por um número DIFERENTE remove o
    /// marcador de confirmado (o novo número nasce não-verificado e exige nova verificação).
    /// false (default) = automação (import/promoção/backfill): telecom confirmado é intocável.
    /// </param>
    public static void AplicarContatos(Patient p, string? principal, string? celular, string? residencial, string? email,
        bool manual = false)
    {
        p.Telecom ??= [];
        UpsertTelefone(p, principal, use: null, rank: 1, manual);
        UpsertTelefone(p, celular, use: ContactPoint.ContactPointUse.Mobile, rank: null, manual);
        UpsertTelefone(p, residencial, use: ContactPoint.ContactPointUse.Home, rank: null, manual);
        UpsertEmail(p, email);
    }

    private static void UpsertTelefone(Patient p, string? valor, ContactPoint.ContactPointUse? use, int? rank, bool manual)
    {
        var digitos = Digitos(valor);
        if (digitos.Length == 0) return; // vazio = no-op (nunca remove; preservação-first)

        bool ehTelefone(ContactPoint t) => t.System == ContactPoint.ContactPointSystem.Phone;
        // Número confirmado é intocável: se o valor recebido é o mesmo de um telecom confirmado,
        // não recria/move — o confirmado já o representa.
        if (p.Telecom.Any(t => ehTelefone(t) && EhConfirmado(t) && MesmoNumero(Digitos(t.Value), digitos)))
            return;

        // Slot gerido: principal identifica-se por Rank==1; os demais por Use (e não-principal).
        var slot = rank == 1
            ? p.Telecom.FirstOrDefault(t => ehTelefone(t) && t.Rank == 1)
            : p.Telecom.FirstOrDefault(t => ehTelefone(t) && t.Use == use && t.Rank != 1);

        if (slot is not null && EhConfirmado(slot))
        {
            // Automação nunca toca num slot confirmado (ex.: import trocando o principal validado).
            if (!manual) return;
            // Edição manual trocando o número: o verificado deixa de valer — remove o marcador
            // e sobrescreve abaixo. Quem atende decide; o novo número exige nova verificação.
            slot.RemoveExtension(ExtContatoConfirmado);
        }

        // Sem slot gerido: reusa um telefone de mesmos dígitos (ex.: o telefone nativo do
        // importado) — nunca colapsa um não-principal no slot principal, nem reusa um confirmado.
        slot ??= p.Telecom.FirstOrDefault(t =>
            ehTelefone(t) && !EhConfirmado(t) && Digitos(t.Value) == digitos && (rank == 1 || t.Rank != 1));

        if (slot is null)
        {
            slot = new ContactPoint { System = ContactPoint.ContactPointSystem.Phone };
            if (rank != 1) slot.Use = use; // Use só em slot novo não-principal; não muta reusados
            p.Telecom.Add(slot);
        }
        // Número DIFERENTE entrando no slot: a marca de inválido era do número antigo, não do novo
        // (antes a troca do telefone pela recepção fazia o número corrigido nascer com o ❗).
        if (slot.Value is not null && !MesmoNumero(Digitos(slot.Value), digitos))
            slot.RemoveExtension(ExtContatoNegado);
        slot.Value = digitos;
        if (rank == 1) slot.Rank = 1;
    }

    private static void UpsertEmail(Patient p, string? email)
    {
        var limpo = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(limpo)) return; // vazio = no-op (preservação-first)
        var slot = p.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email);
        if (slot is not null && EhConfirmado(slot)) return; // confirmado (futuro) é intocável
        if (slot is null) p.Telecom.Add(new ContactPoint { System = ContactPoint.ContactPointSystem.Email, Value = limpo });
        else slot.Value = limpo;
    }

    /// <summary>
    /// Carimba (idempotente) o <paramref name="numero"/> como CONFIRMADO no Patient e remove o
    /// marcador de qualquer outro telecom (1 contato confirmado por pessoa). Se o número ainda não
    /// existir no telecom, adiciona-o como principal. Usado pela validação por OTP e pelo backfill.
    /// Reconcilia DDI: <c>contato_validado</c> guarda "55…", o FHIR guarda nacional (match por sufixo).
    /// </summary>
    public static void MarcarTelefoneConfirmado(Patient p, string numero, DateTimeOffset em)
    {
        var alvo = Digitos(numero);
        if (alvo.Length < 8) return;
        p.Telecom ??= [];

        var confirmado = p.Telecom.FirstOrDefault(t =>
            t.System == ContactPoint.ContactPointSystem.Phone && MesmoNumero(Digitos(t.Value), alvo));
        if (confirmado is null)
        {
            confirmado = new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = alvo.Length > 11 ? alvo[^11..] : alvo, // forma nacional aproximada
            };
            p.Telecom.Add(confirmado);
        }

        // 1 confirmado por pessoa: remove marcador dos outros e a marca do principal (rank1) dos demais.
        foreach (var t in p.Telecom.Where(t => t.System == ContactPoint.ContactPointSystem.Phone))
        {
            if (!ReferenceEquals(t, confirmado))
            {
                t.RemoveExtension(ExtContatoConfirmado);
                t.Rank = null; // o confirmado passa a ser o único principal
            }
        }

        confirmado.Rank = 1; // o confirmado é o contato principal (memória: só o principal é validável)
        confirmado.RemoveExtension(ExtContatoConfirmado);
        confirmado.AddExtension(ExtContatoConfirmado, new FhirDateTime(em));
        // Verificação positiva vence a negação anterior: o número foi provado por OTP AGORA.
        confirmado.RemoveExtension(ExtContatoNegado);
    }

    /// <summary>
    /// Carimba (idempotente) o telecom que casa com <paramref name="numero"/> como NEGADO — quem
    /// atende disse que não é o paciente. NÃO cria telecom novo (adicionar um número que sabemos
    /// errado seria piorar o cadastro) e NÃO remove nada (regra: contato só acumula; quem corrige
    /// é a recepção, pela pendência). Devolve true se algum telecom foi marcado.
    /// </summary>
    public static bool MarcarTelefoneNegado(Patient p, string numero, DateTimeOffset em)
    {
        var alvo = Digitos(numero);
        if (alvo.Length < 8 || p.Telecom is null) return false;

        var marcado = false;
        foreach (var t in p.Telecom.Where(t =>
            t.System == ContactPoint.ContactPointSystem.Phone && MesmoNumero(Digitos(t.Value), alvo)))
        {
            t.RemoveExtension(ExtContatoNegado);
            t.AddExtension(ExtContatoNegado, new FhirDateTime(em));
            marcado = true;
        }
        return marcado;
    }

    /// <summary>
    /// Retira a marca de NEGADO/inválido do telecom que casa com <paramref name="numero"/> — a
    /// recepção deu a denúncia por improcedente (pendência ignorada). Devolve true se tirou algo.
    /// </summary>
    public static bool DesmarcarTelefoneNegado(Patient p, string numero)
    {
        var alvo = Digitos(numero);
        if (alvo.Length < 8 || p.Telecom is null) return false;
        var tirou = false;
        foreach (var t in p.Telecom.Where(t =>
            t.System == ContactPoint.ContactPointSystem.Phone && MesmoNumero(Digitos(t.Value), alvo)
            && t.GetExtension(ExtContatoNegado) is not null))
        {
            t.RemoveExtension(ExtContatoNegado);
            tirou = true;
        }
        return tirou;
    }

    /// <summary>Telecom NEGADO do Patient (número em dígitos + instante), ou null.</summary>
    public static (string Numero, DateTimeOffset? Em)? TelefoneNegado(Patient p)
    {
        var t = p.Telecom?.FirstOrDefault(x =>
            x.System == ContactPoint.ContactPointSystem.Phone && x.GetExtension(ExtContatoNegado) is not null);
        var digitos = Digitos(t?.Value);
        if (t is null || digitos.Length < 8) return null;

        DateTimeOffset? em = null;
        if (t.GetExtension(ExtContatoNegado)?.Value is FhirDateTime fd)
        {
            try { em = fd.ToDateTimeOffset(TimeSpan.Zero); }
            catch { /* carimbo ilegível não invalida a negação */ }
        }
        return (digitos, em);
    }

    private static bool EhConfirmado(ContactPoint t) => t.GetExtension(ExtContatoConfirmado) is not null;

    /// <summary>
    /// Telecom CONFIRMADO do Patient (número em dígitos + instante da validação), ou null.
    /// É a fonte única do "telefone verificado" — substitui a antiga tabela contato_validado.
    /// </summary>
    public static (string Numero, DateTimeOffset? Em)? TelefoneConfirmado(Patient p)
    {
        var t = p.Telecom?.FirstOrDefault(x =>
            x.System == ContactPoint.ContactPointSystem.Phone && EhConfirmado(x));
        var digitos = Digitos(t?.Value);
        if (t is null || digitos.Length < 8) return null;

        DateTimeOffset? em = null;
        if (t.GetExtension(ExtContatoConfirmado)?.Value is FhirDateTime fd)
        {
            try { em = fd.ToDateTimeOffset(TimeSpan.Zero); }
            catch { /* carimbo ilegível não invalida o confirmado */ }
        }
        return (digitos, em);
    }

    /// <summary>true se o <paramref name="numero"/> é o telecom confirmado do Patient (tolera DDI).</summary>
    public static bool TelefoneEstaConfirmado(Patient p, string numero) =>
        TelefoneConfirmado(p) is { } c && MesmoNumero(c.Numero, Digitos(numero));

    /// <summary>Mesmo número tolerando DDI (um é sufixo do outro), com guarda de tamanho.</summary>
    private static bool MesmoNumero(string a, string b) =>
        a.Length >= 8 && b.Length >= 8
        && (a.EndsWith(b, StringComparison.Ordinal) || b.EndsWith(a, StringComparison.Ordinal));

    // ---------------- Campos editados (painel vence no reimport) ----------------

    /// <summary>Adiciona chaves ao marcador de campos editados pelo painel (idempotente, união).</summary>
    public static void MarcarEditados(Patient p, IEnumerable<string> campos)
    {
        var atuais = CamposEditados(p);
        foreach (var c in campos)
            if (!string.IsNullOrWhiteSpace(c)) atuais.Add(c);
        p.RemoveExtension(ExtCamposEditados);
        if (atuais.Count > 0)
            p.AddExtension(ExtCamposEditados, new FhirString(string.Join(",", atuais.OrderBy(x => x, StringComparer.Ordinal))));
    }

    public static HashSet<string> CamposEditados(Patient p)
    {
        var raw = (p.GetExtension(ExtCamposEditados)?.Value as FhirString)?.Value;
        return string.IsNullOrWhiteSpace(raw)
            ? []
            : [.. raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    /// <summary>
    /// MERGE/PRESERVE da importação (ADR-0020 #1/#2): sobrepõe em <paramref name="novo"/> (recém-construído
    /// do Oracle, prestes a ir pro PUT) o que o painel/hub possui e o Oracle NÃO deve sobrescrever:
    /// (1) o blob proprietário; (2) os campos EDITADOS no painel (painel vence); (3) telefones CONFIRMADOS.
    /// O resto (identidade, filiação não-editada, extras crus) segue do Oracle.
    /// </summary>
    public static void PreservarDoExistente(Patient novo, Patient atual)
    {
        // 1. Blob proprietário (clínico/extras do smsmarica; Oracle não tem).
        if (atual.GetExtension(ExtPayloadBlob) is { Value: FhirString blob })
        {
            novo.RemoveExtension(ExtPayloadBlob);
            novo.AddExtension(ExtPayloadBlob, new FhirString(blob.Value));
        }

        // 2. Campos editados no painel — Oracle não sobrescreve. SEMPRE clona do 'atual' (nunca
        //    aliasa a lista/objeto do recurso lido do hub — evita mutação-durante-enumeração e
        //    aliasing entre 'novo' e 'atual').
        var editados = CamposEditados(atual);
        if (editados.Contains("nome"))
        {
            // Correção de nome oficial pelo painel (feature "Verificar") vence o Oracle.
            var oficial = atual.Name?.FirstOrDefault(n => n.Use == HumanName.NameUse.Official);
            if (oficial is not null)
            {
                novo.Name ??= [];
                novo.Name.RemoveAll(n => n.Use == HumanName.NameUse.Official);
                novo.Name.Insert(0, (HumanName)oficial.DeepCopy());
            }
        }
        if (editados.Contains("nomeSocial"))
        {
            novo.Name ??= [];
            novo.Name.RemoveAll(n => n.Use == HumanName.NameUse.Nickname);
            var apelido = atual.Name?.FirstOrDefault(n => n.Use == HumanName.NameUse.Nickname);
            if (apelido is not null) novo.Name.Add((HumanName)apelido.DeepCopy());
        }
        if (editados.Contains("endereco"))
            novo.Address = atual.Address is null ? null : [.. atual.Address.Select(a => (Address)a.DeepCopy())];
        if (editados.Contains("estadoCivil"))
            novo.MaritalStatus = (CodeableConcept?)atual.MaritalStatus?.DeepCopy();
        if (editados.Contains("filiacao"))
            novo.Contact = atual.Contact is null ? null : [.. atual.Contact.Select(c => (Patient.ContactComponent)c.DeepCopy())];
        // Telecom por SISTEMA: e-mail editado não congela o telefone do Oracle (e vice-versa).
        if (editados.Contains("telefone") || editados.Contains("email"))
        {
            var preservarPhone = editados.Contains("telefone");
            var preservarEmail = editados.Contains("email");
            bool gerido(ContactPoint t) =>
                (preservarPhone && t.System == ContactPoint.ContactPointSystem.Phone)
                || (preservarEmail && t.System == ContactPoint.ContactPointSystem.Email);
            novo.Telecom ??= [];
            novo.Telecom.RemoveAll(gerido); // tira do Oracle o(s) sistema(s) que o painel gere
            foreach (var t in (atual.Telecom ?? []).Where(gerido).ToList())
                novo.Telecom.Add((ContactPoint)t.DeepCopy());
        }
        if (editados.Count > 0) MarcarEditados(novo, editados);

        // 3. TODO contato que o hub tem e a origem não trouxe. Ver PreservarContatos.
        PreservarContatos(novo, atual);

        // 4. Telefones CONFIRMADOS — sempre preservados. Idempotente inclusive no retry: remove
        //    QUALQUER confirmado já presente em 'novo' (de uma tentativa anterior) e injeta os do
        //    hub clonados, tirando o número correspondente vindo do Oracle.
        novo.Telecom ??= [];
        novo.Telecom.RemoveAll(EhConfirmado);
        foreach (var conf in (atual.Telecom ?? [])
                     .Where(t => t.System == ContactPoint.ContactPointSystem.Phone && EhConfirmado(t)).ToList())
        {
            novo.Telecom.RemoveAll(x => x.System == ContactPoint.ContactPointSystem.Phone
                && MesmoNumero(Digitos(x.Value), Digitos(conf.Value)));
            novo.Telecom.Add((ContactPoint)conf.DeepCopy());
        }

        // Com um confirmado presente, ele é o ÚNICO principal: telefone vindo do prontuário
        // externo fica num slot secundário (rank demovido), nunca disputa o rank 1.
        if (novo.Telecom.Any(t => t.System == ContactPoint.ContactPointSystem.Phone && EhConfirmado(t)))
        {
            foreach (var t in novo.Telecom.Where(t =>
                         t.System == ContactPoint.ContactPointSystem.Phone && !EhConfirmado(t) && t.Rank == 1))
                t.Rank = null;
        }
    }

    /// <summary>System das marcas de qualidade de dado (buscáveis por <c>_tag</c>).</summary>
    public const string SysQualidade = "urn:smsmarica:qualidade";

    /// <summary>Marca de "não dá para unir esta pessoa a outra base": entrou sem CPF (ADR-0041).</summary>
    public const string TagIdentidadeIncompleta = "identidade-incompleta";

    /// <summary>
    /// Tira a marca de identidade incompleta quando o recurso <b>final</b> tem CPF válido.
    ///
    /// <para>Os três conectores (Salux, Klinikos, SER) carimbam a marca quando a SUA leitura veio
    /// sem CPF — correto, cada um só responde pelo que viu. Mas o recurso gravado é a união: o
    /// CPF pode chegar de outra base, ou já estar no hub, e <c>UnirIdentifiers</c> o traz. A marca
    /// ficava mesmo assim. Medido em 10/08/2026: <b>1.445 pacientes com CPF ainda marcados como
    /// sem CPF</b> — o que estraga justamente a busca que a marca existe para servir
    /// (<c>GET /fhir/Patient?_tag=urn:smsmarica:qualidade|identidade-incompleta</c>).</para>
    ///
    /// <para>Roda depois da união de identifiers, e olha o CPF pelo dígito verificador: entrar com
    /// "00000000000" no identifier não desmarca ninguém.</para>
    /// </summary>
    public static void RevisarIdentidadeIncompleta(Patient p)
    {
        if (p.Meta?.Tag is not { Count: > 0 }) return;
        var cpf = p.Identifier?.FirstOrDefault(i => i.System == SystemCpf)?.Value;
        if (!Integracoes.Pep.CpfPep.Valido(cpf)) return;
        p.Meta.Tag.RemoveAll(t => t.System == SysQualidade && t.Code == TagIdentidadeIncompleta);
    }

    /// <summary>
    /// <b>Nenhuma importação apaga contato do hub. Nunca.</b> Todo telefone/e-mail que existe em
    /// <paramref name="atual"/> e que a origem não trouxe é carregado para
    /// <paramref name="novo"/>.
    ///
    /// <para><b>Por que isto existe (incidente de 10/08/2026).</b> Até aqui só o telefone
    /// CONFIRMADO era preservado; o resto vinha da origem, e origem que não fala apagava. O SER
    /// escancarou: ele é um formulário, o mesmo cidadão aparece em várias solicitações e o campo
    /// telefone vem em branco quando não perguntaram. Uma solicitação sem telefone conciliou por
    /// cima e levou embora <b>2.771 pacientes</b> que tinham número — inclusive números que o
    /// Salux tinha trazido, sem relação nenhuma com o SER. A trilha nem denunciava, porque
    /// remoção não gerava linha.</para>
    ///
    /// <para><b>Vale para fonte parcial e para prontuário completo.</b> Para o resto da demografia
    /// a régua continua a de sempre — campo que sumiu no Salux some no hub, e é
    /// <see cref="CompletarVazios"/> que abre exceção só para fonte parcial. Contato não entra
    /// nessa régua: um número a menos é uma pessoa que a Secretaria deixa de conseguir avisar, e
    /// nenhuma importação tem informação suficiente para afirmar que um telefone deixou de
    /// existir. Quem remove contato é o painel, com gente decidindo.</para>
    ///
    /// <para><b>O rank 1 continua sendo da origem.</b> O principal do hub que não veio na origem
    /// entra como secundário (rank limpo) em vez de disputar o slot — preserva-se o número, não a
    /// precedência. Só quando a origem não trouxe principal nenhum o do hub segue principal.</para>
    /// </summary>
    private static void PreservarContatos(Patient novo, Patient atual)
    {
        if (atual.Telecom is not { Count: > 0 }) return;
        novo.Telecom ??= [];

        var origemTemPrincipal = novo.Telecom.Any(t =>
            t.System == ContactPoint.ContactPointSystem.Phone && t.Rank == 1);

        foreach (var doHub in atual.Telecom.ToList())
        {
            if (JaRepresentado(novo, doHub)) continue;

            var clone = (ContactPoint)doHub.DeepCopy();
            // O principal é de quem falou agora; o herdado vira secundário.
            if (clone.System == ContactPoint.ContactPointSystem.Phone
                && clone.Rank == 1 && origemTemPrincipal)
            {
                clone.Rank = null;
            }
            novo.Telecom.Add(clone);
        }
    }

    /// <summary>
    /// O contato do hub já está no recurso que vai ser gravado? Telefone casa por dígitos
    /// (tolerando DDI, como no resto do arquivo); e-mail, por texto normalizado. Comparar por
    /// dígitos e não por objeto é o que impede o mesmo número de voltar duplicado em slots
    /// diferentes a cada importação.
    /// </summary>
    private static bool JaRepresentado(Patient novo, ContactPoint doHub)
    {
        if (doHub.System == ContactPoint.ContactPointSystem.Phone)
        {
            var digitos = Digitos(doHub.Value);
            if (digitos.Length == 0) return true; // telecom vazio não é contato — não carrega lixo
            return novo.Telecom.Any(t => t.System == ContactPoint.ContactPointSystem.Phone
                && MesmoNumero(Digitos(t.Value), digitos));
        }

        var valor = doHub.Value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(valor)) return true;
        return novo.Telecom.Any(t => t.System == doHub.System
            && string.Equals(t.Value?.Trim().ToLowerInvariant(), valor, StringComparison.Ordinal));
    }

    /// <summary>
    /// Completa o recurso montado por uma <b>fonte PARCIAL</b> com o que só o hub tem.
    ///
    /// <para><b>Por que existe.</b> <see cref="PreservarDoExistente"/> guarda o blob, os campos
    /// editados no painel e o telefone confirmado — e substitui todo o resto pelo que o mapper
    /// montou. Isso é certo para Salux e Klinikos, que são prontuário completo: campo que sumiu
    /// na origem deve sumir no hub. É <b>errado</b> para uma fonte que só conhece um pedaço da
    /// pessoa. O SER, por exemplo, traz endereço em 19.594 das 25.439 solicitações; mandar o
    /// Patient sem endereço apagaria o que o Salux tinha trazido.</para>
    ///
    /// <para>Aqui vazio significa <b>"não sei"</b>, nunca "está vazio" — a mesma régua de
    /// <see cref="AplicarContatos"/>. Fonte parcial só acrescenta; para remover dado do hub
    /// existe o painel.</para>
    ///
    /// <para><b>Telecom e identifier ficam de fora daqui</b> porque são união em TODA importação,
    /// parcial ou não: <see cref="PreservarContatos"/> carrega os contatos e <c>UnirIdentifiers</c>
    /// acumula as chaves, ambos dentro de <see cref="PreservarDoExistente"/>. Repetir a união aqui
    /// desfaria o rank do confirmado que ele acabou de acertar.
    /// <b>Este parágrafo já foi mentira</b>: dizia que telecom "já era união" quando só o
    /// confirmado sobrevivia, e foi assim que 2.771 pacientes perderam o telefone em 10/08/2026.</para>
    /// </summary>
    public static void CompletarVazios(Patient novo, Patient atual)
    {
        if (novo.Name?.Any(n => n.Use == HumanName.NameUse.Official) != true
            && atual.Name?.FirstOrDefault(n => n.Use == HumanName.NameUse.Official) is { } oficial)
        {
            (novo.Name ??= []).Insert(0, (HumanName)oficial.DeepCopy());
        }

        if (novo.Name?.Any(n => n.Use == HumanName.NameUse.Nickname) != true
            && atual.Name?.FirstOrDefault(n => n.Use == HumanName.NameUse.Nickname) is { } apelido)
        {
            (novo.Name ??= []).Add((HumanName)apelido.DeepCopy());
        }

        if (string.IsNullOrWhiteSpace(novo.BirthDate)) novo.BirthDate = atual.BirthDate;

        // `Unknown` é o "não informado" do FHIR: vale como vazio, senão a fonte parcial rebaixaria
        // um sexo conhecido para desconhecido.
        if (novo.Gender is null or AdministrativeGender.Unknown && atual.Gender is not null)
            novo.Gender = atual.Gender;

        novo.MaritalStatus ??= (CodeableConcept?)atual.MaritalStatus?.DeepCopy();
        novo.Deceased ??= (DataType?)atual.Deceased?.DeepCopy();

        if (novo.Address is not { Count: > 0 } && atual.Address is { Count: > 0 })
            novo.Address = [.. atual.Address.Select(a => (Address)a.DeepCopy())];

        // Contact por PARENTESCO: a fonte parcial pode trazer a mãe e não o responsável.
        foreach (var c in atual.Contact ?? [])
        {
            var codigos = CodigosDeParentesco(c);
            var jaTem = codigos.Count == 0
                ? novo.Contact?.Any(x => CodigosDeParentesco(x).Count == 0) == true
                : novo.Contact?.Any(x => CodigosDeParentesco(x).Overlaps(codigos)) == true;
            if (!jaTem) (novo.Contact ??= []).Add((Patient.ContactComponent)c.DeepCopy());
        }

        // Extensions do hub que a fonte parcial não conhece (geolocalização, marcadores de
        // qualidade de outros conectores). O blob e os campos-editados já vieram de
        // PreservarDoExistente; re-adicionar aqui seria duplicar.
        foreach (var ext in atual.Extension ?? [])
        {
            if (novo.GetExtension(ext.Url) is null) novo.Extension.Add((Extension)ext.DeepCopy());
        }
    }

    private static HashSet<string> CodigosDeParentesco(Patient.ContactComponent c) =>
        [.. (c.Relationship ?? [])
            .SelectMany(r => r.Coding ?? [])
            .Select(cd => cd.Code)
            .Where(cd => !string.IsNullOrEmpty(cd))
            .Select(cd => cd!)];

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
