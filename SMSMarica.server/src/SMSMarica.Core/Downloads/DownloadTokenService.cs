using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Exames;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Downloads;

public enum DownloadTokenEstado { Valido, Usado, Expirado, Inexistente }

public sealed record DownloadLinkDto(Guid Token, string Url, DateTime ExpiraEm);
public sealed record DownloadTokenStatusDto(DownloadTokenEstado Estado, string? Descricao);
public sealed record DownloadArquivo(byte[] Bytes, string NomeArquivo, string ContentType);

/// <summary>
/// Links públicos de download de uso único e validade configurável (dias na Config de
/// Laudo). Enviados ao paciente (ex.: WhatsApp). Baixado 1x — ou expirado — o link para
/// de funcionar; daí em diante o cidadão acessa pelo app.
/// </summary>
public interface IDownloadTokenService
{
    /// <summary>Gera um link de download do EXAME COMPLETO (capa+imagens+laudo) de uma solicitação.</summary>
    Task<DownloadLinkDto> GerarExameCompletoAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>Estado do token SEM consumir (para a página pública decidir baixar vs "expirou").</summary>
    Task<DownloadTokenStatusDto> ObterStatusAsync(Guid token, CancellationToken cancellationToken = default);

    /// <summary>Consome o token (marca como usado) e devolve o arquivo. <c>null</c> se inválido/usado/expirado.</summary>
    Task<DownloadArquivo?> ConsumirAsync(Guid token, string? ip, CancellationToken cancellationToken = default);
}

public sealed class DownloadTokenService(
    SmsMaricaDbContext db,
    ILaudoConfiguracaoService configuracaoLaudo,
    IExameCompletoPdfService exameCompleto,
    ISolicitacoesExameService solicitacoes,
    IUsuarioAtualAccessor usuarioAtual,
    IConfiguration configuration) : IDownloadTokenService
{
    private const string TipoExameCompleto = "exame-completo";

    public async Task<DownloadLinkDto> GerarExameCompletoAsync(
        Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        // Valida que a solicitação existe (lança NaoEncontrado se não).
        _ = await solicitacoes.ObterPorIdAsync(solicitacaoExameId, cancellationToken);

        var cfg = await configuracaoLaudo.ObterAsync(cancellationToken);
        var dias = Math.Clamp(cfg.DownloadLinkValidadeDias, 1, 365);

        var token = new DownloadToken
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoExameCompleto,
            ReferenciaId = solicitacaoExameId,
            ExpiraEm = DateTime.UtcNow.AddDays(dias),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.DownloadTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);

        return new DownloadLinkDto(token.Id, MontarUrl(token.Id), token.ExpiraEm);
    }

    public async Task<DownloadTokenStatusDto> ObterStatusAsync(Guid token, CancellationToken cancellationToken = default)
    {
        var t = await db.DownloadTokens.AsNoTracking().FirstOrDefaultAsync(x => x.Id == token, cancellationToken);
        if (t is null) return new DownloadTokenStatusDto(DownloadTokenEstado.Inexistente, null);
        if (t.UsadoEm is not null) return new DownloadTokenStatusDto(DownloadTokenEstado.Usado, null);
        if (t.ExpiraEm <= DateTime.UtcNow) return new DownloadTokenStatusDto(DownloadTokenEstado.Expirado, null);

        string? descricao = null;
        try
        {
            var s = await solicitacoes.ObterPorIdAsync(t.ReferenciaId, cancellationToken);
            descricao = string.IsNullOrWhiteSpace(s.TipoExameNome) ? null : s.TipoExameNome;
        }
        catch (NaoEncontradoException) { /* descrição é opcional */ }

        return new DownloadTokenStatusDto(DownloadTokenEstado.Valido, descricao);
    }

    public async Task<DownloadArquivo?> ConsumirAsync(Guid token, string? ip, CancellationToken cancellationToken = default)
    {
        var t = await db.DownloadTokens.FirstOrDefaultAsync(x => x.Id == token, cancellationToken);
        if (t is null || t.UsadoEm is not null || t.ExpiraEm <= DateTime.UtcNow) return null;

        // Gera o arquivo ANTES de marcar como usado — se a geração falhar, o link
        // continua válido para nova tentativa.
        var bytes = t.Tipo switch
        {
            TipoExameCompleto => await exameCompleto.GerarAsync(t.ReferenciaId, cancellationToken),
            _ => throw new ConflitoException("download.tipo_desconhecido", "Tipo de download não suportado."),
        };

        t.UsadoEm = DateTime.UtcNow;
        t.UsadoIp = ip is { Length: > 64 } ? ip[..64] : ip;
        await db.SaveChangesAsync(cancellationToken);

        return new DownloadArquivo(bytes, $"exame-{t.ReferenciaId}.pdf", "application/pdf");
    }

    private string MontarUrl(Guid token)
    {
        var appBase = (configuration["Publico:AppBaseUrl"] ?? "https://app.smsmarica.online").TrimEnd('/');
        return $"{appBase}/documento/{token}";
    }
}
