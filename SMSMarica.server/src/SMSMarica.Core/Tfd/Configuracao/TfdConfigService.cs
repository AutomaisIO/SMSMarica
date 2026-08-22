using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Inteligencia.Seguranca;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Geo;
using SMSMarica.Data.Entities.Notificacoes;

namespace SMSMarica.Core.Tfd.Configuracao;

public sealed class TfdConfigService(SmsMaricaDbContext db, IProtetorSegredos protetor) : ITfdConfigService
{
    // ---------------- Google ----------------
    public async Task<TfdConfigGoogleDto> ObterGoogleAsync(CancellationToken ct = default)
    {
        var c = await ObterOuCriarGoogleAsync(ct);
        return new TfdConfigGoogleDto(c.BaseUrl, !string.IsNullOrEmpty(c.ApiKeyCifrada), c.Ativo);
    }

    public async Task AtualizarGoogleAsync(AtualizarTfdConfigGoogleRequest request, CancellationToken ct = default)
    {
        var c = await ObterOuCriarGoogleAsync(ct);
        c.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? c.BaseUrl : request.BaseUrl.Trim();
        c.Ativo = request.Ativo;
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            c.ApiKeyCifrada = protetor.Proteger(request.ApiKey.Trim());
        }
        c.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<TfdGoogleContexto> ObterGoogleContextoAsync(CancellationToken ct = default)
    {
        var c = await db.GeoConfiguracao.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new ValidacaoException("google.nao_configurado", "Integração Google Maps ainda não configurada.");
        if (!c.Ativo) throw new ValidacaoException("google.inativo", "Integração Google Maps está desativada.");
        if (string.IsNullOrEmpty(c.ApiKeyCifrada)) throw new ValidacaoException("google.sem_chave", "Chave da API Google não configurada.");
        return new TfdGoogleContexto(c.BaseUrl, protetor.Revelar(c.ApiKeyCifrada));
    }

    // ---------------- WhatsApp ----------------
    public async Task<TfdConfigWhatsAppDto> ObterWhatsAppAsync(CancellationToken ct = default)
    {
        var c = await ObterOuCriarWhatsAppAsync(ct);
        return new TfdConfigWhatsAppDto(
            c.BaseUrl, c.PhoneNumberId, c.WabaId,
            !string.IsNullOrEmpty(c.TokenCifrado),
            !string.IsNullOrEmpty(c.VerifyTokenCifrado),
            !string.IsNullOrEmpty(c.AppSecretCifrado),
            c.Ativo,
            c.ZapBaseUrl,
            !string.IsNullOrEmpty(c.ZapTokenCifrado),
            !string.IsNullOrEmpty(c.ZapSegredoWebhookCifrado),
            c.ZapAtivo);
    }

    public async Task AtualizarWhatsAppAsync(AtualizarTfdConfigWhatsAppRequest request, CancellationToken ct = default)
    {
        var c = await ObterOuCriarWhatsAppAsync(ct);
        c.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? c.BaseUrl : request.BaseUrl.Trim();
        c.PhoneNumberId = string.IsNullOrWhiteSpace(request.PhoneNumberId) ? c.PhoneNumberId : request.PhoneNumberId.Trim();
        c.WabaId = string.IsNullOrWhiteSpace(request.WabaId) ? c.WabaId : request.WabaId.Trim();
        c.Ativo = request.Ativo;
        if (!string.IsNullOrWhiteSpace(request.Token)) c.TokenCifrado = protetor.Proteger(request.Token.Trim());
        if (!string.IsNullOrWhiteSpace(request.VerifyToken)) c.VerifyTokenCifrado = protetor.Proteger(request.VerifyToken.Trim());
        if (!string.IsNullOrWhiteSpace(request.AppSecret)) c.AppSecretCifrado = protetor.Proteger(request.AppSecret.Trim());
        c.ZapBaseUrl = string.IsNullOrWhiteSpace(request.ZapBaseUrl) ? c.ZapBaseUrl : request.ZapBaseUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.ZapToken)) c.ZapTokenCifrado = protetor.Proteger(request.ZapToken.Trim());
        if (!string.IsNullOrWhiteSpace(request.ZapSegredoWebhook)) c.ZapSegredoWebhookCifrado = protetor.Proteger(request.ZapSegredoWebhook.Trim());
        c.ZapAtivo = request.ZapAtivo;
        c.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<TfdWhatsAppContexto> ObterWhatsAppContextoAsync(CancellationToken ct = default)
    {
        var c = await db.WhatsAppConfiguracao.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new ValidacaoException("whatsapp.nao_configurado", "Integração WhatsApp ainda não configurada.");
        if (!c.Ativo) throw new ValidacaoException("whatsapp.inativo", "Integração WhatsApp está desativada.");
        // Com o envio pelo Automais.Zap, o token da Meta deixa de ser obrigatório aqui — é
        // justamente o ponto de sair do App antigo sem precisar de credencial da Meta na
        // instância. O PhoneNumberId continua exigido: é ele que identifica a linha.
        var viaZap = c.ZapAtivo
                     && !string.IsNullOrWhiteSpace(c.ZapBaseUrl)
                     && !string.IsNullOrEmpty(c.ZapTokenCifrado);

        if (string.IsNullOrWhiteSpace(c.PhoneNumberId))
            throw new ValidacaoException("whatsapp.incompleto", "Phone Number ID do WhatsApp não configurado.");
        if (!viaZap && string.IsNullOrEmpty(c.TokenCifrado))
            throw new ValidacaoException("whatsapp.incompleto", "Token do WhatsApp não configurado.");

        return new TfdWhatsAppContexto(
            c.BaseUrl,
            string.IsNullOrEmpty(c.TokenCifrado) ? string.Empty : protetor.Revelar(c.TokenCifrado),
            c.PhoneNumberId,
            c.WabaId,
            string.IsNullOrEmpty(c.VerifyTokenCifrado) ? null : protetor.Revelar(c.VerifyTokenCifrado),
            string.IsNullOrEmpty(c.AppSecretCifrado) ? null : protetor.Revelar(c.AppSecretCifrado),
            c.ZapBaseUrl,
            string.IsNullOrEmpty(c.ZapTokenCifrado) ? null : protetor.Revelar(c.ZapTokenCifrado),
            c.ZapAtivo,
            string.IsNullOrEmpty(c.ZapSegredoWebhookCifrado) ? null : protetor.Revelar(c.ZapSegredoWebhookCifrado));
    }

    private async Task<GeoConfiguracao> ObterOuCriarGoogleAsync(CancellationToken ct)
    {
        var c = await db.GeoConfiguracao.FirstOrDefaultAsync(ct);
        if (c is not null) return c;
        c = new GeoConfiguracao { Id = Guid.CreateVersion7(), CriadoEm = DateTime.UtcNow };
        db.GeoConfiguracao.Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }

    private async Task<WhatsAppConfiguracao> ObterOuCriarWhatsAppAsync(CancellationToken ct)
    {
        var c = await db.WhatsAppConfiguracao.FirstOrDefaultAsync(ct);
        if (c is not null) return c;
        c = new WhatsAppConfiguracao { Id = Guid.CreateVersion7(), CriadoEm = DateTime.UtcNow };
        db.WhatsAppConfiguracao.Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }
}
