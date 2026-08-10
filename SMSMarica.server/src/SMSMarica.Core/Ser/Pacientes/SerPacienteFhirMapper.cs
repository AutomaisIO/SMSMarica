using Hl7.Fhir.Model;
using SMSMarica.Core.Integracoes.Pep;
using SMSMarica.Data.Entities.Ser;
// Alias: o namespace Ser.Pacientes sombreia Core.Pacientes.
using PatientMergeFhir = SMSMarica.Core.Pacientes.Fhir.PatientMergeFhir;

namespace SMSMarica.Core.Ser.Pacientes;

/// <summary>
/// A solicitação espelhada do SER → <c>Patient</c> FHIR.
///
/// <para><b>O SER é fonte PARCIAL, e é isso que muda tudo aqui.</b> Salux e Klinikos são
/// prontuário completo: o que sumiu lá deve sumir no hub. O SER é um formulário preenchido por
/// quem abriu a solicitação — medido em 10/08/2026 sobre 25.439 solicitações vivas: CNS em
/// 25.438, CPF em 21.520, nascimento em 20.558, nome da mãe em 20.557, CEP em 19.594 e telefone
/// de WhatsApp em 11.826. Campo vazio aqui significa <b>"não perguntaram"</b>, jamais "não tem".
/// Por isso o upsert roda com <c>fonteParcial: true</c> e este mapper nunca emite campo vazio:
/// o que ele não souber, o hub mantém.</para>
///
/// <para><b>O telefone de WhatsApp vai para o slot de celular</b> (<c>telecom use=mobile</c>) por
/// ser exatamente o campo que a plataforma já usa para falar por zap — a escolha do número no
/// envio é <c>verificado &gt; celular &gt; principal</c>. E como a conciliação é automação, um
/// número já <b>verificado é intocável</b>: <c>AplicarContatos(manual: false)</c> recusa mexer em
/// telecom com o marcador de confirmado (ADR-0020 #2). É a regra "só troca o zap se o telefone
/// não estiver verificado", garantida por invariante e não por um <c>if</c> deste arquivo.</para>
///
/// <para><b>Somente leitura no SER</b> continua valendo: aqui não se fala com o SER. A fonte é a
/// nossa própria tabela espelho.</para>
///
/// <para><b>Campos que o espelho tem e este mapper ignora, medidos e não presumidos:</b>
/// <c>etnia</c> vem vazia nas 25.439 solicitações — a coluna existe, o SER nunca a preenche, e
/// mapear raça/cor a partir dela seria código morto. <c>idade_texto</c> é derivada do
/// nascimento, que já entra.</para>
/// </summary>
public static class SerPacienteFhirMapper
{
    public const string SysCpf = "https://fhir.saude.gov.br/sid/cpf";
    public const string SysCns = "https://fhir.saude.gov.br/sid/cns";

    /// <summary>Slug da origem — compõe o <c>meta.source</c>, como em toda base (ADR-0009).</summary>
    public const string Slug = "ser-sesrj";

    public const string Source = "https://smsmarica.saude.marica/source/" + Slug;

    /// <summary>
    /// Monta o Patient com <b>só o que o SER sabe</b>. O que vier vazio simplesmente não entra no
    /// recurso — quem completa é <c>PatientMergeFhir.CompletarVazios</c> a partir do hub.
    /// </summary>
    public static Patient Construir(SerSolicitacao s)
    {
        // Só CPF VÁLIDO por dígito verificador vira identifier e âncora. "00000000000" é
        // preenchimento clássico de campo obrigatório e, como chave nacional, FUNDIRIA duas
        // pessoas num Patient só (ADR-0041, adendo de 04/08).
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

        // Telefones: contato → principal, whatsapp → celular, residencial → residencial.
        // AplicarContatos trata vazio como no-op e recusa tocar em telecom confirmado.
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

        // Sem CPF válido não há como afirmar que este é o mesmo "João" de outra base. Entra
        // assim mesmo (ADR-0041: o silêncio é pior), mas declarado.
        if (cpf.Length == 0) MarcarIdentidadeIncompleta(p);

        return p;
    }

    /// <summary>
    /// Endereço residencial, só com o que veio. Devolve <c>null</c> quando o SER não trouxe nada
    /// de endereço — emitir um Address vazio apagaria o do hub mesmo em modo parcial, porque a
    /// lista deixaria de estar vazia.
    /// </summary>
    private static Address? Endereco(SerSolicitacao s)
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

    /// <summary>
    /// O SER escreve o sexo por extenso e sem padrão fixo. Só reconhece o que é inequívoco: o
    /// resto vira <c>null</c>, e null aqui é "não sei" — em modo parcial, o hub mantém o que tem.
    /// </summary>
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
