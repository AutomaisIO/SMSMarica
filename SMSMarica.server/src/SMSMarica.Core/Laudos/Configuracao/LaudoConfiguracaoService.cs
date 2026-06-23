using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Laudos.Configuracao.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Laudos.Configuracao;

public sealed class LaudoConfiguracaoService(SmsMaricaDbContext db, IHtmlSanitizer sanitizer) : ILaudoConfiguracaoService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IHtmlSanitizer _sanitizer = sanitizer;

    public async Task<LaudoConfiguracaoDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var c = await _db.LaudoConfiguracoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == LaudoConfiguracao.IdSingleton, cancellationToken);
        return c is null ? Vazia() : ParaDto(c);
    }

    public async Task<LaudoConfiguracaoDto> SalvarAsync(
        Guid usuarioId,
        SalvarLaudoConfiguracaoRequest request,
        CancellationToken cancellationToken = default)
    {
        var c = await _db.LaudoConfiguracoes
            .FirstOrDefaultAsync(x => x.Id == LaudoConfiguracao.IdSingleton, cancellationToken);

        var novo = c is null;
        c ??= new LaudoConfiguracao { Id = LaudoConfiguracao.IdSingleton };

        c.CabecalhoHtml = _sanitizer.Sanitize(request.CabecalhoHtml ?? string.Empty);
        c.CabecalhoJson = string.IsNullOrWhiteSpace(request.CabecalhoJson) ? "{}" : request.CabecalhoJson;
        c.RodapeHtml = _sanitizer.Sanitize(request.RodapeHtml ?? string.Empty);
        c.RodapeJson = string.IsNullOrWhiteSpace(request.RodapeJson) ? "{}" : request.RodapeJson;
        c.AtualizadoPorUsuarioId = usuarioId;
        c.AtualizadoEm = DateTime.UtcNow;

        if (novo) _db.LaudoConfiguracoes.Add(c);
        await _db.SaveChangesAsync(cancellationToken);

        return ParaDto(c);
    }

    private static LaudoConfiguracaoDto Vazia() => new(string.Empty, "{}", string.Empty, "{}", null);

    private static LaudoConfiguracaoDto ParaDto(LaudoConfiguracao c) =>
        new(c.CabecalhoHtml, c.CabecalhoJson, c.RodapeHtml, c.RodapeJson, c.AtualizadoEm);
}
