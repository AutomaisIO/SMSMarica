using Hl7.Fhir.Model;
using SMSMais.Core.Integracoes.Pep;
using SMSMais.Data.Entities.Sernit;
// Alias: o namespace Sernit.Pacientes sombreia SMSMais.Core.Pacientes.
using PatientMergeFhir = SMSMais.Core.Pacientes.Fhir.PatientMergeFhir;

namespace SMSMais.Core.Sernit.Pacientes;

/// <summary>
/// A solicitação espelhada do SERNIT → <c>Patient</c> FHIR. Espelho do <c>SerPacienteFhirMapper</c>.
///
/// <para><b>O SERNIT é fonte PARCIAL</b> (um formulário, não um prontuário): campo vazio significa
/// "não perguntaram", jamais "não tem". Por isso o upsert roda com <c>fonteParcial: true</c> e este
/// mapper nunca emite campo vazio — o que ele não souber, o hub mantém.</para>
///
/// <para>O <b>Telefone SMS</b> do SERNIT (o de notificação) vai para o slot de celular; número já
/// verificado é intocável (<c>AplicarContatos(manual: false)</c>).</para>
/// </summary>
public static class SernitPacienteFhirMapper
{
    public const string SysCpf = "https://fhir.saude.gov.br/sid/cpf";
    public const string SysCns = "https://fhir.saude.gov.br/sid/cns";

    /// <summary>Slug da origem — compõe o <c>meta.source</c> (ADR-0009).</summary>
    public const string Slug = "sernit-niteroi";

    public const string Source = "https://smsmarica.saude.marica/source/" + Slug;

    public static Patient Construir(SernitSolicitacao s)
    {
        var cpf = CpfPep.Valido(s.Cpf) ? CpfPep.Digitos(s.Cpf) : string.Empty;
        var cns = Digitos(s.Cns);

        var ident = new List<Identifier>();
        if (cpf.Length > 0) ident.Add(new Identifier(SysCpf, cpf));
        if (cns.Length > 0) ident.Add(new Identifier(SysCns, cns));

        var p = new Patient
        {
            Meta = new Meta { Source = Source },
            Identifier = ident,
            Active = true,
        };

        if (Texto(s.PacienteNome) is { } nome)
            p.Name = [new HumanName { Use = HumanName.NameUse.Official, Text = nome }];

        if (Genero(s.Sexo) is { } sexo) p.Gender = sexo;
        if (s.DataNascimento is { } nasc) p.BirthDate = nasc.ToString("yyyy-MM-dd");

        PatientMergeFhir.AplicarContatos(
            p,
            principal: s.TelefoneContato,
            celular: s.TelefoneWhatsapp,
            residencial: s.TelefoneResidencial,
            email: null,
            manual: false);

        if (Texto(s.NomeMae) is { } mae)
            PatientMergeFhir.UpsertContato(p, "MTH", mae);

        if (Endereco(s) is { } endereco) p.Address = [endereco];

        if (cpf.Length == 0) MarcarIdentidadeIncompleta(p);

        return p;
    }

    private static Address? Endereco(SernitSolicitacao s)
    {
        var linha = string.Join(' ', new[] { Texto(s.TipoLogradouro), Texto(s.Logradouro), Texto(s.Numero) }
            .OfType<string>()).Trim();
        var cep = Digitos(s.Cep);

        if (linha.Length == 0 && cep.Length == 0
            && Texto(s.Bairro) is null && Texto(s.MunicipioPaciente) is null && Texto(s.Uf) is null)
        {
            return null;
        }

        var a = new Address { Use = Address.AddressUse.Home, Type = Address.AddressType.Physical };
        if (linha.Length > 0) a.Line = [linha];
        if (Texto(s.Complemento) is { } comp) a.Line = [.. a.Line ?? [], comp];
        if (Texto(s.Bairro) is { } bairro) a.District = bairro;
        if (Texto(s.MunicipioPaciente) is { } cidade) a.City = cidade;
        if (Texto(s.Uf) is { } uf) a.State = uf;
        if (cep.Length > 0) a.PostalCode = cep;
        return a;
    }

    private static AdministrativeGender? Genero(string? sexo) =>
        (sexo ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "M" or "MASCULINO" => AdministrativeGender.Male,
            "F" or "FEMININO" => AdministrativeGender.Female,
            _ => null,
        };

    private static void MarcarIdentidadeIncompleta(Patient p)
    {
        p.Meta ??= new Meta();
        p.Meta.Tag ??= [];
        const string sys = "urn:smsmarica:qualidade";
        const string cod = "identidade-incompleta";
        if (p.Meta.Tag.Any(t => t.System == sys && t.Code == cod)) return;
        p.Meta.Tag.Add(new Coding(sys, cod) { Display = "Sem CPF — não é possível unir a outras bases" });
    }

    private static string? Texto(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
}
