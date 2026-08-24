using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Medicos.Assinatura;

public sealed class AssinaturaMedicoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IAssinaturaMedicoService
{
    public async Task<AssinaturaMedicoDto?> ObterAsync(Guid medicoId, CancellationToken cancellationToken = default)
    {
        var a = await db.AssinaturasMedico.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MedicoId == medicoId && x.ExcluidoEm == null, cancellationToken);
        return a is null ? null : ParaDto(a);
    }

    public async Task<AssinaturaMedicoDto> SalvarAsync(
        Guid medicoId, SalvarAssinaturaMedicoRequest request, CancellationToken cancellationToken = default)
    {
        var agora = DateTime.UtcNow;
        var usuarioId = usuarioAtual.UsuarioId;

        var assinatura = await db.AssinaturasMedico
            .FirstOrDefaultAsync(x => x.MedicoId == medicoId && x.ExcluidoEm == null, cancellationToken);

        if (assinatura is null)
        {
            assinatura = new AssinaturaMedico
            {
                Id = Guid.CreateVersion7(),
                MedicoId = medicoId,
                ImagemBase64 = request.ImagemBase64,
                ContentType = request.ContentType,
                Formato = request.Formato,
                CriadoEm = agora,
                CriadoPor = usuarioId,
            };
            db.AssinaturasMedico.Add(assinatura);
        }
        else
        {
            assinatura.ImagemBase64 = request.ImagemBase64;
            assinatura.ContentType = request.ContentType;
            assinatura.Formato = request.Formato;
            assinatura.AtualizadoEm = agora;
            assinatura.AtualizadoPor = usuarioId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ParaDto(assinatura);
    }

    public async Task RemoverAsync(Guid medicoId, CancellationToken cancellationToken = default)
    {
        var assinatura = await db.AssinaturasMedico
            .FirstOrDefaultAsync(x => x.MedicoId == medicoId && x.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(AssinaturaMedico), medicoId);

        var agora = DateTime.UtcNow;
        assinatura.ExcluidoEm = agora;
        assinatura.ExcluidoPor = usuarioAtual.UsuarioId;
        assinatura.AtualizadoEm = agora;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AssinaturaMedicoDto ParaDto(AssinaturaMedico a) => new(
        a.MedicoId, a.ImagemBase64, a.ContentType, a.Formato, a.AtualizadoEm ?? a.CriadoEm);
}
