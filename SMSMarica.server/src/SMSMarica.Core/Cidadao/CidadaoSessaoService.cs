using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Cidadao;

public sealed class CidadaoSessaoService(
    SmsMaisDbContext db,
    IPacienteTokenService tokens,
    IConfiguration config,
    ILogger<CidadaoSessaoService> logger) : ICidadaoSessaoService
{
    public async Task<(int Links, int Sessoes)> RevogarTodosAcessosAsync(
        string motivo, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;

        // Expirar = ninguém mais troca o link por sessão (mesmo caminho do "Reenviar", sem o
        // filtro por solicitação). Não apagamos a linha: o histórico de quem clicou e quando
        // é justamente o que permite investigar para onde a mensagem foi parar.
        var links = await db.CidadaoLoginLinks
            .Where(l => l.ExpiraEm > agora)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.ExpiraEm, agora), ct);

        // Quem já entrou com um link (possivelmente errado) perde o acesso agora.
        var sessoes = await db.CidadaoSessoes
            .Where(s => s.RevogadaEm == null && s.ExpiraEm > agora)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevogadaEm, agora), ct);

        logger.LogWarning(
            "REVOGAÇÃO GLOBAL de acessos do cidadão: {Links} link(s) expirado(s), {Sessoes} sessão(ões) revogada(s). Motivo: {Motivo}",
            links, sessoes, motivo);

        return (links, sessoes);
    }

    public async Task<(string Token, DateTime ExpiraEm)> AbrirSessaoAsync(
        Guid patientId, string nome, string cpf, string canal,
        string? dispositivo, string? ip, CancellationToken ct = default)
    {
        var cpfDigitos = Digitos(cpf);

        // 1. Garante o cidadao_acesso (fonte da verdade = CPF; PatientId é a referência ao hub).
        var acesso = await db.CidadaoAcessos.FirstOrDefaultAsync(a => a.PatientId == patientId, ct);
        if (acesso is null)
        {
            acesso = new CidadaoAcesso
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                Cpf = cpfDigitos,
                Ativo = true,
                CriadoEm = DateTime.UtcNow,
            };
            db.CidadaoAcessos.Add(acesso);
        }
        else if (!acesso.Ativo)
        {
            // Mantém a regra de bloqueio temporário coerente com Usuario.Ativo.
            acesso.Ativo = true;
            acesso.AtualizadoEm = DateTime.UtcNow;
        }

        // 2. Múltiplas sessões permitidas: NÃO revoga as anteriores no login. No iOS o
        //    PWA instalado tem storage separado do Safari/Chrome; a antiga política
        //    single-device derrubava a sessão do PWA ao logar em outra superfície,
        //    gerando 401 recorrente. Logout explícito (RevogarAsync) segue revogando
        //    a sessão específica; as demais expiram naturalmente.
        var agora = DateTime.UtcNow;

        // 3. Cria a nova sessão. O Id é o jti que vai no token.
        var dias = config.GetValue("Tfd:Cidadao:SessaoDias", defaultValue: 30);
        var expira = agora.AddDays(dias);
        var sessao = new CidadaoSessao
        {
            Id = Guid.NewGuid(),
            CidadaoAcessoId = acesso.Id,
            Canal = canal,
            Dispositivo = Truncar(dispositivo, 255),
            Ip = Truncar(ip, 64),
            CriadaEm = agora,
            ExpiraEm = expira,
        };
        db.CidadaoSessoes.Add(sessao);
        await db.SaveChangesAsync(ct);

        var token = tokens.Gerar(patientId, nome, cpfDigitos, sessao.Id, expira);
        return (token, expira);
    }

    public async Task<AcessoCidadaoValidacao> ValidarAcessoAsync(
        Guid sessaoJti, Guid patientId, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var versao = TermoConsentimento.VersaoVigente;
        var r = await db.CidadaoSessoes
            .Where(s => s.Id == sessaoJti
                && s.RevogadaEm == null
                && s.ExpiraEm > agora
                && s.CidadaoAcesso.PatientId == patientId
                && s.CidadaoAcesso.Ativo)
            .Select(s => new
            {
                Consentido = s.CidadaoAcesso.Consentimentos
                    .Any(c => c.Versao == versao && c.RevogadoEm == null),
            })
            .FirstOrDefaultAsync(ct);

        return r is null
            ? new AcessoCidadaoValidacao(false, false)
            : new AcessoCidadaoValidacao(true, r.Consentido);
    }

    public async Task<IReadOnlyList<Dtos.AcessoCidadaoDto>> ListarAcessosAsync(
        Guid patientId, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        return await db.CidadaoSessoes
            .Where(s => s.CidadaoAcesso.PatientId == patientId)
            .OrderByDescending(s => s.CriadaEm)
            .Select(s => new Dtos.AcessoCidadaoDto(
                s.Id,
                s.Canal,
                s.Dispositivo,
                s.Ip,
                s.CriadaEm,
                s.ExpiraEm,
                s.RevogadaEm,
                s.RevogadaEm == null && s.ExpiraEm > agora))
            .ToListAsync(ct);
    }

    public async Task RevogarAsync(Guid sessaoJti, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        await db.CidadaoSessoes
            .Where(s => s.Id == sessaoJti && s.RevogadaEm == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevogadaEm, agora), ct);
    }

    private static string Digitos(string? v) =>
        string.IsNullOrEmpty(v) ? string.Empty : new string([.. v.Where(char.IsDigit)]);

    private static string? Truncar(string? v, int max) =>
        string.IsNullOrEmpty(v) ? v : v.Length <= max ? v : v[..max];
}
