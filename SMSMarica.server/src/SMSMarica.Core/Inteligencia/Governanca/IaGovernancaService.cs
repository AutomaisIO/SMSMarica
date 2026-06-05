using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Core.Inteligencia.Governanca;

/// <summary>
/// Implementa a governança do aprendizado: lista/desativa instruções aprendidas e audita
/// o histórico de correções. Desativar é não-destrutivo (Ativo=false + soft-delete).
/// </summary>
public sealed class IaGovernancaService(
    SmsMaricaDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IIaGovernancaService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<AprendizadoDto>> ListarAprendizadosAsync(
        Guid? fonteId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.IaAprendizados.AsNoTracking()
            .Where(a => a.ExcluidoEm == null);

        if (fonteId is { } fId)
        {
            query = query.Where(a => a.FonteId == fId);
        }

        return await query
            .OrderByDescending(a => a.CriadoEm)
            .Select(a => new AprendizadoDto(
                a.Id,
                a.FonteId,
                a.Fonte!.Nome,
                a.Tipo.ToString(),
                a.Origem.ToString(),
                a.Conteudo,
                a.Ativo,
                a.CriadoEm))
            .ToListAsync(cancellationToken);
    }

    public async Task DesativarAprendizadoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var aprendizado = await _db.IaAprendizados
            .FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(IaAprendizado), id);

        aprendizado.Ativo = false;
        aprendizado.ExcluidoEm = DateTime.UtcNow;
        aprendizado.ExcluidoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CorrecaoDto>> ListarCorrecoesAsync(
        Guid? fonteId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.IaCorrecoes.AsNoTracking().AsQueryable();

        if (fonteId is { } fId)
        {
            query = query.Where(c => c.Consulta!.FonteId == fId);
        }

        return await query
            .OrderByDescending(c => c.CriadoEm)
            .Select(c => new CorrecaoDto(
                c.Id,
                c.ConsultaId,
                c.AprendizadoId,
                c.Consulta!.Pergunta,
                c.ErroOriginal,
                c.SqlAntes,
                c.SqlDepois,
                c.InstrucaoGerada,
                c.CriadoEm,
                c.RevisadoEm,
                c.RemovidoEm))
            .ToListAsync(cancellationToken);
    }
}
