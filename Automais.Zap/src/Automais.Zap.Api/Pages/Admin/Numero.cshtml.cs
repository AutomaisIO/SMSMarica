using Automais.Zap.Api.Infra;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Automais.Zap.Api.Pages.Admin;

/// <summary>
/// Diagnóstico de um número: o que a Meta diz sobre ele e como o relay o trata.
///
/// Só lê. Toda a configuração (ligar/desligar, destino próprio) continua na tela do WABA —
/// duplicar formulário de escrita em duas telas é como se cria divergência de regra.
/// </summary>
public sealed class NumeroModel(ZapDbContext db, EscopoUsuario escopo, IGraphMetaClient graph) : PageModel
{
    public Data.Entities.Numero? Alvo { get; private set; }
    public Data.Entities.Waba? Waba { get; private set; }

    /// <summary>Retrato vindo da Meta. Nulo quando a consulta falhou — ver <see cref="ErroGraph"/>.</summary>
    public NumeroDetalheMeta? NaMeta { get; private set; }
    public string? ErroGraph { get; private set; }

    public string? DestinoEfetivo => string.IsNullOrWhiteSpace(Alvo?.UrlDestinoOverride)
        ? Waba?.UrlDestino
        : Alvo!.UrlDestinoOverride;

    public bool DestinoProprio => !string.IsNullOrWhiteSpace(Alvo?.UrlDestinoOverride);

    /// <summary>As três condições que precisam valer juntas para um evento deste número sair daqui.</summary>
    public bool Entregando => Alvo is { Ativo: true }
                              && Waba is { RoteamentoAtivo: true }
                              && !string.IsNullOrWhiteSpace(DestinoEfetivo);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Alvo = await db.Numeros.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, ct);
        if (Alvo is null) return NotFound();

        Waba = await db.Wabas.AsNoTracking().FirstOrDefaultAsync(w => w.Id == Alvo.WabaId, ct);
        if (Waba is null) return NotFound();

        // Fail-closed: número é de um tenant, e tenant que o usuário não enxerga não existe.
        if (!await escopo.PodeVerAsync(Waba.TenantId, ct)) return Forbid();

        var r = await graph.ObterNumeroAsync(Alvo.PhoneNumberId, ct);
        if (r.Sucesso) NaMeta = r.Valor;
        else ErroGraph = r.Erro;

        return Page();
    }

    /// <summary>
    /// Critério de qualificação para conta comercial oficial.
    /// <c>Atendido == null</c> significa "a API não responde isto" — e não "não atende".
    /// O token do System User não tem <c>business_management</c>, então verificação do
    /// portfólio e 2FA só dá para conferir no Gerenciador.
    /// </summary>
    public sealed record Criterio(string Texto, bool? Atendido, string Nota);

    public IReadOnlyList<Criterio> Criterios()
    {
        var m = NaMeta;
        var noApp = m?.NoAppBusiness == true;
        var plataforma = m?.PlatformType is { Length: > 0 } p && !p.Equals("NOT_APPLICABLE", StringComparison.OrdinalIgnoreCase);

        return
        [
            new("Em conformidade com a Política de Mensagens do WhatsApp Business",
                null,
                m is null
                    ? "Não consultado."
                    : $"A API não afirma isso. Indício: qualidade {m.QualityRating ?? "?"} e status {m.Status ?? "?"}."),

            new("Registrado na Plataforma há pelo menos 30 dias",
                null,
                Alvo is null
                    ? "Não consultado."
                    : $"A Meta não expõe a data de registro. No relay este número existe desde "
                      + $"{Alvo.CriadoEm.ToLocalTime():dd/MM/yyyy} — o que é o dia em que foi sincronizado, não o do registro."),

            new("Portfólio empresarial verificado",
                null,
                "Exige business_management, que o token do System User não tem. "
                + "Confira em Meta Business Suite → Configurações → Central de Segurança → Verificação da empresa."),

            new("Verificação em duas etapas ligada no número",
                null,
                "Não é exposto pela API do número. Confira no Gerenciador do WhatsApp, aba "
                + "\"Verificação em duas etapas\" do número."),

            new("Nome de exibição aprovado",
                m is null ? null : string.Equals(m.NameStatus, "APPROVED", StringComparison.OrdinalIgnoreCase),
                m is null ? "Não consultado." : $"name_status = {m.NameStatus ?? "(vazio)"}."),

            // Não é critério, é impedimento: a Meta não concede OBA a número do app.
            new("Não é um número do app WhatsApp Business",
                m is null ? null : !noApp && plataforma,
                m is null
                    ? "Não consultado."
                    : noApp
                        ? "Este número está no app WhatsApp Business — a Meta não concede OBA a número de app."
                        : $"platform_type = {m.PlatformType ?? "(vazio)"}."),
        ];
    }
}
