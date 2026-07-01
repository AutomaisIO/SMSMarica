using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Cidadao;

/// <summary>
/// "Magic-link" de login do cidadão: gera um token de uso único e validade curta
/// (dias configuráveis) para autenticar em 1 clique via WhatsApp, e o troca por uma
/// sessão normal do cidadão. Pensado para idosos, sem abrir mão da LGPD (uso único,
/// TTL curto, o app limpa o token da URL após entrar).
/// </summary>
public interface ICidadaoLoginLinkService
{
    /// <summary>Gera um magic-link que loga o paciente da solicitação e cai no exame.</summary>
    Task<MagicLinkDto> GerarParaSolicitacaoAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>Troca o token por sessão (uso único). <c>null</c> se inválido/usado/expirado.</summary>
    Task<RespostaMagicLinkDto?> TrocarAsync(Guid token, string? dispositivo, string? ip, CancellationToken cancellationToken = default);
}

public sealed class CidadaoLoginLinkService(
    SmsMaricaDbContext db,
    ILaudoConfiguracaoService configuracaoLaudo,
    IPacientesService pacientes,
    ISolicitacoesExameService solicitacoes,
    ICidadaoSessaoService sessoes,
    IUsuarioAtualAccessor usuarioAtual,
    IConfiguration configuration) : ICidadaoLoginLinkService
{
    private const string DestinoPadrao = "/exames";

    public async Task<MagicLinkDto> GerarParaSolicitacaoAsync(
        Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var s = await solicitacoes.ObterPorIdAsync(solicitacaoExameId, cancellationToken);

        var cpf = Digitos(s.PacienteCpf);
        if (cpf.Length == 0)
        {
            var p = await pacientes.ObterPorIdAsync(s.PacienteId, cancellationToken);
            cpf = Digitos(p.Cpf);
        }
        if (cpf.Length != 11)
            throw new ConflitoException("magiclink.sem_cpf", "Paciente sem CPF válido para gerar o link de acesso.");

        var cfg = await configuracaoLaudo.ObterAsync(cancellationToken);
        var dias = Math.Clamp(cfg.MagicLinkValidadeDias, 1, 30);

        var link = new CidadaoLoginLink
        {
            Id = Guid.CreateVersion7(),
            PatientId = s.PacienteId,
            Cpf = cpf,
            Destino = DestinoPadrao,
            ExpiraEm = DateTime.UtcNow.AddDays(dias),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.CidadaoLoginLinks.Add(link);
        await db.SaveChangesAsync(cancellationToken);

        return new MagicLinkDto(link.Id, MontarUrl(link.Id), link.ExpiraEm);
    }

    public async Task<RespostaMagicLinkDto?> TrocarAsync(
        Guid token, string? dispositivo, string? ip, CancellationToken cancellationToken = default)
    {
        var link = await db.CidadaoLoginLinks.FirstOrDefaultAsync(x => x.Id == token, cancellationToken);
        if (link is null || link.UsadoEm is not null || link.ExpiraEm <= DateTime.UtcNow) return null;

        // Nome vem do hub FHIR (não persistimos nome no smsmarica).
        var paciente = await pacientes.ObterPorIdAsync(link.PatientId, cancellationToken);
        var nome = string.IsNullOrWhiteSpace(paciente.NomeCompleto) ? "Paciente" : paciente.NomeCompleto;

        link.UsadoEm = DateTime.UtcNow;
        link.UsadoIp = ip is { Length: > 64 } ? ip[..64] : ip;

        // Abre a sessão normal do cidadão (mesma do OTP) e salva (persiste também o link).
        var (jwt, _) = await sessoes.AbrirSessaoAsync(
            link.PatientId, nome, link.Cpf, "magic-link", dispositivo, ip, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new RespostaMagicLinkDto(
            jwt,
            new PacienteSessaoDto(link.PatientId, nome, link.Cpf),
            string.IsNullOrWhiteSpace(link.Destino) ? "/" : link.Destino!);
    }

    private string MontarUrl(Guid token)
    {
        var appBase = (configuration["Publico:AppBaseUrl"] ?? "https://app.smsmarica.online").TrimEnd('/');
        return $"{appBase}/entrar/{token}";
    }

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);
}
