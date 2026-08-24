using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Exames;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.SolicitacoesExame;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Downloads;

public enum DownloadTokenEstado { Valido, Usado, Expirado, Inexistente }

public sealed record DownloadLinkDto(Guid Token, string Url, DateTime ExpiraEm);

/// <summary>Estado do link. <c>Descricao</c> só vem depois do CPF conferido — o nome do exame
/// já é dado de saúde e identificaria o titular para quem recebeu o link por engano.</summary>
public sealed record DownloadTokenStatusDto(
    DownloadTokenEstado Estado, string? Descricao, bool RequerCpf = false, int? TentativasRestantes = null);

public sealed record DownloadArquivo(byte[] Bytes, string NomeArquivo, string ContentType);

/// <summary>Resultado da confirmação de CPF: <c>Liberacao</c> null = CPF errado (ou link queimado).</summary>
public sealed record DownloadLiberacaoDto(Guid? Liberacao, int TentativasRestantes, string? Descricao);

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

    /// <summary>Confere o CPF do titular ATUAL do exame e emite a liberação de curta duração.
    /// CPF errado conta tentativa; na 3ª queima o link. Não consome o token.</summary>
    Task<DownloadLiberacaoDto> ConfirmarCpfAsync(Guid token, string? cpf, CancellationToken cancellationToken = default);

    /// <summary>Consome o token (marca como usado) e devolve o arquivo. Exige a
    /// <paramref name="liberacao"/> emitida pela confirmação de CPF. <c>null</c> se
    /// inválido/usado/expirado/sem liberação.</summary>
    Task<DownloadArquivo?> ConsumirAsync(Guid token, Guid? liberacao, string? ip, CancellationToken cancellationToken = default);
}

public sealed class DownloadTokenService(
    SmsMaisDbContext db,
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

    /// <summary>Tentativas de CPF antes de o link ser queimado.</summary>
    private const int MaxTentativasCpf = 3;

    /// <summary>Janela da liberação: tempo entre confirmar o CPF e o arquivo começar a baixar.</summary>
    private static readonly TimeSpan ValidadeLiberacao = TimeSpan.FromMinutes(15);

    public async Task<DownloadTokenStatusDto> ObterStatusAsync(Guid token, CancellationToken cancellationToken = default)
    {
        var t = await db.DownloadTokens.AsNoTracking().FirstOrDefaultAsync(x => x.Id == token, cancellationToken);
        if (t is null) return new DownloadTokenStatusDto(DownloadTokenEstado.Inexistente, null);
        if (t.UsadoEm is not null) return new DownloadTokenStatusDto(DownloadTokenEstado.Usado, null);
        if (t.ExpiraEm <= DateTime.UtcNow) return new DownloadTokenStatusDto(DownloadTokenEstado.Expirado, null);

        // Antes do CPF, NADA sobre o exame: nem o nome do procedimento. Ele já é dado de saúde
        // e, em conjunto com o número que recebeu a mensagem, identificaria o titular.
        return new DownloadTokenStatusDto(
            DownloadTokenEstado.Valido, Descricao: null,
            RequerCpf: true, TentativasRestantes: Math.Max(0, MaxTentativasCpf - t.TentativasCpf));
    }

    public async Task<DownloadLiberacaoDto> ConfirmarCpfAsync(
        Guid token, string? cpf, CancellationToken cancellationToken = default)
    {
        var t = await db.DownloadTokens.FirstOrDefaultAsync(x => x.Id == token, cancellationToken);
        if (t is null || t.UsadoEm is not null || t.ExpiraEm <= DateTime.UtcNow)
            return new DownloadLiberacaoDto(null, 0, null);

        // CPF do titular ATUAL do exame, resolvido na hora (não é snapshot de propósito): se o
        // exame trocar de dono numa correção de identidade, o link antigo para de abrir sozinho.
        string cpfTitular;
        string? descricao = null;
        try
        {
            var s = await solicitacoes.ObterPorIdAsync(t.ReferenciaId, cancellationToken);
            cpfTitular = Digitos(s.PacienteCpf);
            descricao = string.IsNullOrWhiteSpace(s.TipoExameNome) ? null : s.TipoExameNome;
        }
        catch (NaoEncontradoException)
        {
            return new DownloadLiberacaoDto(null, 0, null);
        }

        var informado = Digitos(cpf);
        if (cpfTitular.Length != 11 || !string.Equals(informado, cpfTitular, StringComparison.Ordinal))
        {
            t.TentativasCpf++;
            if (t.TentativasCpf >= MaxTentativasCpf) t.ExpiraEm = DateTime.UtcNow; // queima
            await db.SaveChangesAsync(cancellationToken);
            return new DownloadLiberacaoDto(null, Math.Max(0, MaxTentativasCpf - t.TentativasCpf), null);
        }

        t.Liberacao = Guid.CreateVersion7();
        t.LiberadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new DownloadLiberacaoDto(t.Liberacao, MaxTentativasCpf - t.TentativasCpf, descricao);
    }

    public async Task<DownloadArquivo?> ConsumirAsync(
        Guid token, Guid? liberacao, string? ip, CancellationToken cancellationToken = default)
    {
        var t = await db.DownloadTokens.FirstOrDefaultAsync(x => x.Id == token, cancellationToken);
        if (t is null || t.UsadoEm is not null || t.ExpiraEm <= DateTime.UtcNow) return null;

        // Sem a liberação do CPF não sai arquivo — é o que impede voltar a ser um link anônimo.
        if (t.Liberacao is null || liberacao != t.Liberacao) return null;
        if (t.LiberadoEm is null || DateTime.UtcNow - t.LiberadoEm > ValidadeLiberacao) return null;

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

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
}
