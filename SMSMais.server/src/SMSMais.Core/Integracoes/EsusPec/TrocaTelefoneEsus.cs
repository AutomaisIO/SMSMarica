using System.Globalization;
using System.Text;
using Hl7.Fhir.Model;
using SMSMais.Core.Conversas;
using SMSMais.Core.Pacientes.Fhir;

namespace SMSMais.Core.Integracoes.EsusPec;

/// <summary>O que fazer com o paciente depois de ler o celular dele no e-SUS PEC (ADR-0067).</summary>
public enum DecisaoTelefoneEsus
{
    /// <summary>O PEC tem outro celular válido — troca o principal.</summary>
    Trocar,
    /// <summary>Telefone validado (OTP) no cadastro — nunca é alterado.</summary>
    MantemVerificado,
    /// <summary>O PEC não tem celular válido.</summary>
    EsusSemCelular,
    /// <summary>O PEC traz o MESMO número que falhou / foi negado — o e-SUS também está errado.</summary>
    EsusTambemErrado,
    /// <summary>O celular do PEC já é o principal do cadastro (alguém já corrigiu).</summary>
    JaEOPrincipal,
    /// <summary>O celular do PEC é o confirmado (próprio) de OUTRA pessoa, sem sobrenome em comum.</summary>
    NumeroDeOutraPessoa,
}

/// <summary>
/// Regras puras (sem banco nem rede) da correção de telefone pelo e-SUS PEC — as MESMAS usadas à mão em
/// 02–04/10/2026 nos 4.064 pacientes (laboratório <c>Automais.esus</c>):
/// validado nunca muda; número do PEC igual ao que falhou = "e-SUS também errado"; número confirmado
/// por outra pessoa só passa com sobrenome em comum (celular da família); na troca o antigo vai para o
/// HISTÓRICO (<c>period.end</c>, sem rank) — nunca some — e o novo entra como principal com
/// <c>contato-origem = esus-pec</c>.
/// </summary>
public static class TrocaTelefoneEsus
{
    public const string Origem = "esus-pec";

    private static readonly HashSet<string> Particulas = ["DA", "DE", "DO", "DAS", "DOS", "E", "D"];

    /// <summary>Celular brasileiro em forma nacional (DDD + 9 dígitos), ou null se não for um.</summary>
    public static string? CelularNacional(string? telefone)
    {
        var d = new string([.. (telefone ?? "").Where(char.IsDigit)]);
        if (d.Length is 12 or 13 && d.StartsWith("55", StringComparison.Ordinal)) d = d[2..];
        return d.Length == 11 && d[2] == '9' ? d : null;
    }

    public static IEnumerable<ContactPoint> TelefonesAtivos(Patient p) =>
        (p.Telecom ?? []).Where(t => t.System == ContactPoint.ContactPointSystem.Phone && !PatientMergeFhir.EhAposentado(t));

    /// <summary>Principal de fato: o rank 1, ou o primeiro telefone ativo.</summary>
    public static ContactPoint? Principal(Patient p)
    {
        var ativos = TelefonesAtivos(p).ToList();
        return ativos.FirstOrDefault(t => t.Rank == 1) ?? ativos.FirstOrDefault();
    }

    public static bool TemTelefoneValidado(Patient p) =>
        TelefonesAtivos(p).Any(t => t.GetExtension(PatientMergeFhir.ExtContatoConfirmado) is not null);

    public static bool EhNumeroNegado(Patient p, string numero) =>
        (p.Telecom ?? []).Any(t => t.System == ContactPoint.ContactPointSystem.Phone
            && t.GetExtension(PatientMergeFhir.ExtContatoNegado) is not null
            && TelefoneWhatsApp.MesmoNumero(t.Value, numero));

    /// <summary>Sobrenomes (sem o primeiro nome e sem partículas) em comum — o "é da família".</summary>
    public static bool SobrenomeEmComum(string? nomeA, string? nomeB)
    {
        static HashSet<string> Sobrenomes(string? n)
        {
            var t = Normalizar(n).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return [.. t.Skip(1).Where(x => x.Length > 1 && !Particulas.Contains(x))];
        }
        var a = Sobrenomes(nomeA);
        return a.Count > 0 && a.Overlaps(Sobrenomes(nomeB));
    }

    /// <summary>Decide, na ordem das regras. <paramref name="furados"/> = números que falharam/foram negados.
    /// <paramref name="confirmadoPorOutro"/> = o número do PEC é o confirmado (próprio) de outra pessoa
    /// SEM sobrenome em comum (quem chama já aplicou a exceção da família).</summary>
    public static DecisaoTelefoneEsus Decidir(Patient p, string? celularPec, IEnumerable<string> furados, bool confirmadoPorOutro)
    {
        if (TemTelefoneValidado(p)) return DecisaoTelefoneEsus.MantemVerificado;
        if (CelularNacional(celularPec) is not { } cel) return DecisaoTelefoneEsus.EsusSemCelular;
        if (EhNumeroNegado(p, cel) || furados.Any(f => TelefoneWhatsApp.MesmoNumero(f, cel)))
            return DecisaoTelefoneEsus.EsusTambemErrado;
        if (Principal(p) is { } pr && TelefoneWhatsApp.MesmoNumero(pr.Value, cel)) return DecisaoTelefoneEsus.JaEOPrincipal;
        if (confirmadoPorOutro) return DecisaoTelefoneEsus.NumeroDeOutraPessoa;
        return DecisaoTelefoneEsus.Trocar;
    }

    /// <summary>
    /// Aplica a troca no Patient: o principal atual e os números que falharam vão para o histórico
    /// (<c>period.end = quando</c>, sem rank — continuam no cadastro); o celular do PEC vira o principal.
    /// Se o número já existia no cadastro (ex.: secundário ou aposentado), é reaproveitado; senão entra
    /// novo com <c>contato-origem = esus-pec</c> e <c>period.start</c>. Devolve quantos foram ao histórico
    /// e o número anterior (para a auditoria).
    /// </summary>
    public static (int ParaHistorico, string? Anterior) AplicarTroca(
        Patient p, string celularPec, IEnumerable<string> furados, DateTimeOffset quando)
    {
        var cel = CelularNacional(celularPec) ?? throw new ArgumentException("celular inválido", nameof(celularPec));
        p.Telecom ??= [];
        var principal = Principal(p);
        var anterior = principal?.Value;
        var listaFurados = furados.ToList();
        var fim = new FhirDateTime(quando);
        var aposentados = 0;

        foreach (var t in TelefonesAtivos(p).ToList())
        {
            if (TelefoneWhatsApp.MesmoNumero(t.Value, cel)) continue;
            var furado = ReferenceEquals(t, principal) || listaFurados.Any(f => TelefoneWhatsApp.MesmoNumero(f, t.Value));
            if (!furado) continue;
            t.Period ??= new Period();
            t.Period.EndElement = fim;
            t.Rank = null;
            aposentados++;
        }
        foreach (var t in p.Telecom.Where(t => t.System == ContactPoint.ContactPointSystem.Phone && t.Rank == 1))
            t.Rank = null;

        var existente = p.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone
            && TelefoneWhatsApp.MesmoNumero(t.Value, cel));
        if (existente is not null)
        {
            if (existente.Period is not null) existente.Period.EndElement = null;
            existente.Rank = 1;
        }
        else
        {
            var novo = new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = cel,
                Use = ContactPoint.ContactPointUse.Mobile,
                Rank = 1,
                Period = new Period { StartElement = new FhirDateTime(quando) },
            };
            novo.AddExtension(PatientMergeFhir.ExtContatoOrigem, new FhirString(Origem));
            p.Telecom.Add(novo);
        }
        return (aposentados, anterior);
    }

    private static string Normalizar(string? s)
    {
        var d = (s ?? "").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToUpperInvariant(ch));
        return sb.ToString();
    }
}
