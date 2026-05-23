using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.EstudoAnotacoes.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.EstudoAnotacoes;

public sealed class EstudoAnotacoesService(SmsMaricaDbContext db) : IEstudoAnotacoesService
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<EstudoAnotacaoVersaoDto?> ObterVersaoAtualAsync(
        string studyInstanceUID,
        CancellationToken cancellationToken = default)
    {
        var uid = NormalizarUid(studyInstanceUID);
        var a = await _db.EstudoAnotacoes.AsNoTracking()
            .Include(x => x.Usuario)
            .Where(x => x.StudyInstanceUID == uid)
            .OrderByDescending(x => x.Versao)
            .FirstOrDefaultAsync(cancellationToken);
        return a is null ? null : ParaDto(a);
    }

    public async Task<IReadOnlyList<EstudoAnotacaoVersaoResumoDto>> ListarHistoricoAsync(
        string studyInstanceUID,
        CancellationToken cancellationToken = default)
    {
        var uid = NormalizarUid(studyInstanceUID);
        var lista = await _db.EstudoAnotacoes.AsNoTracking()
            .Include(x => x.Usuario)
            .Where(x => x.StudyInstanceUID == uid)
            .OrderByDescending(x => x.Versao)
            .Select(x => new EstudoAnotacaoVersaoResumoDto(
                x.Id,
                x.Versao,
                x.UsuarioId,
                x.Usuario!.NomeCompleto,
                x.CriadoEm,
                x.Comentario))
            .ToListAsync(cancellationToken);
        return lista;
    }

    public async Task<EstudoAnotacaoVersaoDto> ObterVersaoAsync(
        string studyInstanceUID,
        int versao,
        CancellationToken cancellationToken = default)
    {
        var uid = NormalizarUid(studyInstanceUID);
        var a = await _db.EstudoAnotacoes.AsNoTracking()
            .Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.StudyInstanceUID == uid && x.Versao == versao, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(EstudoAnotacao), $"{uid}@v{versao}");
        return ParaDto(a);
    }

    public async Task<EstudoAnotacaoVersaoDto> SalvarAsync(
        string studyInstanceUID,
        Guid usuarioId,
        SalvarEstudoAnotacaoRequest request,
        CancellationToken cancellationToken = default)
    {
        var uid = NormalizarUid(studyInstanceUID);

        var usuario = await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        // Próxima versão = max(versao) atual + 1. Append-only: nunca atualiza linhas anteriores.
        var proximaVersao = await _db.EstudoAnotacoes
            .Where(x => x.StudyInstanceUID == uid)
            .Select(x => (int?)x.Versao)
            .MaxAsync(cancellationToken) ?? 0;
        proximaVersao++;

        var entidade = new EstudoAnotacao
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = uid,
            Versao = proximaVersao,
            PayloadJson = request.Payload.GetRawText(),
            UsuarioId = usuarioId,
            CriadoEm = DateTime.UtcNow,
            Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim(),
        };

        _db.EstudoAnotacoes.Add(entidade);
        await _db.SaveChangesAsync(cancellationToken);

        return new EstudoAnotacaoVersaoDto(
            entidade.Id,
            entidade.StudyInstanceUID,
            entidade.Versao,
            request.Payload,
            usuario.Id,
            usuario.NomeCompleto,
            entidade.CriadoEm,
            entidade.Comentario);
    }

    private static string NormalizarUid(string studyInstanceUID)
    {
        if (string.IsNullOrWhiteSpace(studyInstanceUID))
        {
            throw new ValidacaoException("estudoAnotacao.studyUid_obrigatorio", "StudyInstanceUID é obrigatório.");
        }
        return studyInstanceUID.Trim();
    }

    private static EstudoAnotacaoVersaoDto ParaDto(EstudoAnotacao a) => new(
        a.Id,
        a.StudyInstanceUID,
        a.Versao,
        JsonSerializer.Deserialize<JsonElement>(a.PayloadJson),
        a.UsuarioId,
        a.Usuario?.NomeCompleto ?? string.Empty,
        a.CriadoEm,
        a.Comentario);
}
