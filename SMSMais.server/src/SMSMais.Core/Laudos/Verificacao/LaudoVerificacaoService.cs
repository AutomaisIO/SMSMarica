using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QRCoder;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Laudos.Verificacao;

/// <inheritdoc />
public sealed class LaudoVerificacaoService(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    IConfiguration configuration) : ILaudoVerificacaoService
{
    public async Task<SeloVerificacaoLaudo> ObterSeloAsync(
        Guid laudoId, bool assinaturaDigital, CancellationToken cancellationToken = default)
    {
        var selo = await ObterOuCriarAsync(laudoId, cancellationToken);
        var url = MontarUrl(selo.Id);
        return new SeloVerificacaoLaudo(selo.Id, url, GerarQrPng(url), assinaturaDigital);
    }

    public async Task<LaudoVerificacaoPublicaDto?> VerificarAsync(Guid codigo, CancellationToken cancellationToken = default)
    {
        var selo = await db.LaudoVerificacoes.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == codigo, cancellationToken);
        if (selo is null) return null;

        var laudo = await db.Laudos.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == selo.LaudoId && !l.Excluido, cancellationToken);
        if (laudo is null) return null;

        var oficial = await db.LaudoAssinaturas.AsNoTracking()
            .Where(a => a.LaudoId == laudo.Id && a.Status == StatusAssinatura.Concluida)
            .Select(a => new { a.Formato, a.CertificadoTitular, a.CertificadoEmissor, a.AssinadoEm })
            .FirstOrDefaultAsync(cancellationToken);

        var substituido = await db.Laudos.AsNoTracking()
            .AnyAsync(l => l.LaudoAnteriorId == laudo.Id && !l.Excluido, cancellationToken);

        var paciente = "—";
        if (laudo.PacienteId is { } pid)
        {
            try
            {
                var p = await pacientes.ObterPorIdAsync(pid, cancellationToken);
                if (!string.IsNullOrWhiteSpace(p.NomeCompleto)) paciente = p.NomeCompleto;
            }
            catch (NaoEncontradoException)
            {
                // Paciente removido do hub: o selo continua dizendo se o documento vale.
            }
        }

        var registro = string.IsNullOrWhiteSpace(laudo.MedicoCrmSnapshot)
            ? string.Empty
            : $"CRM {laudo.MedicoUfCrmSnapshot}/{laudo.MedicoCrmSnapshot}".Trim();

        return new LaudoVerificacaoPublicaDto(
            Liberado: oficial is not null,
            Substituido: substituido,
            PacienteNome: paciente,
            Exame: string.IsNullOrWhiteSpace(laudo.Titulo) ? "Laudo" : laudo.Titulo,
            MedicoNome: laudo.MedicoNomeSnapshot ?? "—",
            MedicoRegistro: registro,
            EmitidoEm: oficial?.AssinadoEm is { } em ? FusoBrasilia.ParaExibicao(em) : null,
            AssinaturaDigital: oficial is not null && !LaudoAssinaturaService.EhCarimboSemCertificado(oficial.Formato),
            CertificadoTitular: oficial?.CertificadoTitular,
            CertificadoEmissor: oficial?.CertificadoEmissor);
    }

    public async Task<byte[]?> ObterPdfOficialAsync(Guid codigo, CancellationToken cancellationToken = default)
    {
        var laudoId = await db.LaudoVerificacoes.AsNoTracking()
            .Where(v => v.Id == codigo)
            .Select(v => (Guid?)v.LaudoId)
            .FirstOrDefaultAsync(cancellationToken);
        if (laudoId is null) return null;

        return await db.LaudoAssinaturas.AsNoTracking()
            .Where(a => a.LaudoId == laudoId && a.Status == StatusAssinatura.Concluida && a.PdfAssinado != null
                        && db.Laudos.Any(l => l.Id == a.LaudoId && !l.Excluido))
            .Select(a => a.PdfAssinado)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<LaudoVerificacao> ObterOuCriarAsync(Guid laudoId, CancellationToken ct)
    {
        var existente = await db.LaudoVerificacoes.FirstOrDefaultAsync(v => v.LaudoId == laudoId, ct);
        if (existente is not null) return existente;

        var novo = new LaudoVerificacao
        {
            // v4 de propósito (122 bits aleatórios): o v7 expõe o instante de criação.
            Id = Guid.NewGuid(),
            LaudoId = laudoId,
            CriadoEm = DateTime.UtcNow,
        };
        db.LaudoVerificacoes.Add(novo);
        try
        {
            await db.SaveChangesAsync(ct);
            return novo;
        }
        catch (DbUpdateException)
        {
            // Corrida: outro request criou o selo entre o SELECT e o INSERT.
            db.Entry(novo).State = EntityState.Detached;
            return await db.LaudoVerificacoes.FirstAsync(v => v.LaudoId == laudoId, ct);
        }
    }

    private string MontarUrl(Guid codigo)
    {
        var baseUrl = (configuration["Publico:BaseUrl"] ?? "https://api.smsmarica.online").TrimEnd('/');
        return $"{baseUrl}/publico/laudos/{codigo}";
    }

    private static byte[] GerarQrPng(string conteudo)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(dados).GetGraphic(10);
    }
}
