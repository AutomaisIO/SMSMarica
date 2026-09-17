using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Resolve uma instância do conector web do Klinikos a partir da sua <see cref="IaFonte"/>
/// (Tipo <see cref="TipoFonte.KlinikosWeb"/>), pelo <c>slug</c>. Decifra a senha com o mesmo
/// <see cref="IProtetorSegredos"/> das outras fontes e interpreta o <c>ParametrosJson</c> via
/// <see cref="KlinikosWebParametros"/>. É AQUI que o conector deixou de depender do store de
/// credenciais de integração (OAuth) — agora é mais uma fonte de prontuário.
/// </summary>
public interface IKlinikosWebFonteResolver
{
    Task<KlinikosFonteResolvida> ResolverAsync(string slug, CancellationToken ct);
}

/// <summary>Instância resolvida: conexão + credenciais reveladas + identidade FHIR + versão declarada.</summary>
public sealed record KlinikosFonteResolvida(
    KlinikosInstancia Instancia, string Usuario, string Senha, string MetaSource, bool WebPrimaria,
    string Build);

public sealed class KlinikosWebFonteResolver(SmsMaisDbContext db, IProtetorSegredos protetor)
    : IKlinikosWebFonteResolver
{
    public async Task<KlinikosFonteResolvida> ResolverAsync(string slug, CancellationToken ct)
    {
        var fonte = await db.Set<IaFonte>().AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.Slug == slug && f.Tipo == TipoFonte.KlinikosWeb && f.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Fonte de prontuário Klinikos (web)", slug);

        if (!fonte.Ativo)
        {
            throw new ValidacaoException("klinikos.fonte_inativa", $"A fonte '{slug}' está inativa.");
        }
        if (string.IsNullOrWhiteSpace(fonte.BaseUrl)
            || string.IsNullOrWhiteSpace(fonte.Usuario)
            || string.IsNullOrWhiteSpace(fonte.SenhaCifrada))
        {
            throw new ValidacaoException(
                "klinikos.fonte_incompleta",
                $"Configure URL, usuário e senha da fonte Klinikos web '{slug}'.");
        }

        var p = KlinikosWebParametros.Resolver(slug, fonte.ParametrosJson);
        var instancia = new KlinikosInstancia(
            slug, new Uri(fonte.BaseUrl!.TrimEnd('/')), p.AppRoot, p.UnidCodigo);

        return new KlinikosFonteResolvida(
            instancia, fonte.Usuario!, protetor.Revelar(fonte.SenhaCifrada!), p.MetaSource, p.WebPrimaria,
            p.Build);
    }
}
