using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Medicos.Assinatura;

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

    public async Task<ModoAssinaturaMedicoDto> ObterModoAsync(Guid medicoId, CancellationToken cancellationToken = default)
    {
        var c = await db.ConfiguracoesAssinaturaMedico.AsNoTracking()
            .FirstOrDefaultAsync(x => x.MedicoId == medicoId, cancellationToken);
        return c is null
            ? new ModoAssinaturaMedicoDto(medicoId, ModoAssinaturaMedico.Desktop, false, null)
            : new ModoAssinaturaMedicoDto(medicoId, c.Modo, true, c.AtualizadoEm ?? c.CriadoEm);
    }

    public async Task<ModoAssinaturaMedicoDto> DefinirModoAsync(
        Guid medicoId, ModoAssinaturaMedico modo, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(modo))
            throw new ValidacaoException("modo", "Modo de assinatura inválido.");

        var agora = DateTime.UtcNow;
        var usuarioId = usuarioAtual.UsuarioId;
        var c = await db.ConfiguracoesAssinaturaMedico
            .FirstOrDefaultAsync(x => x.MedicoId == medicoId, cancellationToken);
        if (c is null)
        {
            c = new ConfiguracaoAssinaturaMedico
            {
                MedicoId = medicoId,
                Modo = modo,
                CriadoEm = agora,
                CriadoPor = usuarioId,
            };
            db.ConfiguracoesAssinaturaMedico.Add(c);
        }
        else
        {
            c.Modo = modo;
            c.AtualizadoEm = agora;
            c.AtualizadoPor = usuarioId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ModoAssinaturaMedicoDto(medicoId, c.Modo, true, c.AtualizadoEm ?? c.CriadoEm);
    }

    private static AssinaturaMedicoDto ParaDto(AssinaturaMedico a) => new(
        a.MedicoId, a.ImagemBase64, a.ContentType, a.Formato, a.AtualizadoEm ?? a.CriadoEm);
}
