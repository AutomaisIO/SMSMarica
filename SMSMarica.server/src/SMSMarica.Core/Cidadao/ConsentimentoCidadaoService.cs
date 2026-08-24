using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Cidadao;

public sealed class ConsentimentoCidadaoService(SmsMaisDbContext db) : IConsentimentoCidadaoService
{
    public async Task<ConsentimentoStatusDto> ObterStatusAsync(Guid patientId, CancellationToken ct = default)
    {
        var versao = TermoConsentimento.VersaoVigente;
        var ativo = await db.CidadaoConsentimentos
            .Where(c => c.CidadaoAcesso.PatientId == patientId && c.Versao == versao && c.RevogadoEm == null)
            .OrderByDescending(c => c.AceitoEm)
            .Select(c => (DateTime?)c.AceitoEm)
            .FirstOrDefaultAsync(ct);

        return new ConsentimentoStatusDto(versao, TermoConsentimento.Texto, ativo is not null, ativo);
    }

    public async Task RegistrarAsync(Guid patientId, string? ip, string? dispositivo, CancellationToken ct = default)
    {
        var acesso = await db.CidadaoAcessos.FirstOrDefaultAsync(a => a.PatientId == patientId, ct)
            ?? throw new NaoEncontradoException(nameof(CidadaoAcesso), patientId);

        var versao = TermoConsentimento.VersaoVigente;
        var jaTem = await db.CidadaoConsentimentos
            .AnyAsync(c => c.CidadaoAcessoId == acesso.Id && c.Versao == versao && c.RevogadoEm == null, ct);
        if (jaTem) return; // idempotente

        db.CidadaoConsentimentos.Add(new CidadaoConsentimento
        {
            Id = Guid.NewGuid(),
            CidadaoAcessoId = acesso.Id,
            Versao = versao,
            TextoHash = TermoConsentimento.Hash,
            AceitoEm = DateTime.UtcNow,
            Ip = Truncar(ip, 64),
            Dispositivo = Truncar(dispositivo, 255),
        });
        await db.SaveChangesAsync(ct);
    }

    private static string? Truncar(string? v, int max) =>
        string.IsNullOrEmpty(v) ? v : v.Length <= max ? v : v[..max];
}
