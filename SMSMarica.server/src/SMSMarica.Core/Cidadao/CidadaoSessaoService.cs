using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Cidadao;

public sealed class CidadaoSessaoService(
    SmsMaricaDbContext db,
    IPacienteTokenService tokens,
    IConfiguration config) : ICidadaoSessaoService
{
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

        // 2. Single-device: revoga todas as sessões ativas anteriores deste acesso.
        var agora = DateTime.UtcNow;
        await db.CidadaoSessoes
            .Where(s => s.CidadaoAcessoId == acesso.Id && s.RevogadaEm == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevogadaEm, agora), ct);

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

    public async Task<bool> SessaoValidaAsync(Guid sessaoJti, Guid patientId, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        return await db.CidadaoSessoes
            .Where(s => s.Id == sessaoJti
                && s.RevogadaEm == null
                && s.ExpiraEm > agora
                && s.CidadaoAcesso.PatientId == patientId
                && s.CidadaoAcesso.Ativo)
            .AnyAsync(ct);
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
