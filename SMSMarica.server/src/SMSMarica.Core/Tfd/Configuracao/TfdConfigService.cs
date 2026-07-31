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
            c.Ativo);
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
        c.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<TfdWhatsAppContexto> ObterWhatsAppContextoAsync(CancellationToken ct = default)
    {
        var c = await db.WhatsAppConfiguracao.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? throw new ValidacaoException("whatsapp.nao_configurado", "Integração WhatsApp ainda não configurada.");
        if (!c.Ativo) throw new ValidacaoException("whatsapp.inativo", "Integração WhatsApp está desativada.");
        if (string.IsNullOrEmpty(c.TokenCifrado) || string.IsNullOrWhiteSpace(c.PhoneNumberId))
            throw new ValidacaoException("whatsapp.incompleto", "Token ou Phone Number ID do WhatsApp não configurados.");
        return new TfdWhatsAppContexto(
            c.BaseUrl,
            protetor.Revelar(c.TokenCifrado),
            c.PhoneNumberId,
            c.WabaId,
            string.IsNullOrEmpty(c.VerifyTokenCifrado) ? null : protetor.Revelar(c.VerifyTokenCifrado),
            string.IsNullOrEmpty(c.AppSecretCifrado) ? null : protetor.Revelar(c.AppSecretCifrado));
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
