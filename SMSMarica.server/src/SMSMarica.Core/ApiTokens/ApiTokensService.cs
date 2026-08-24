using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.ApiTokens.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.ApiTokens;

public sealed class ApiTokensService(SmsMaisDbContext db) : IApiTokensService
{
    private readonly SmsMaisDbContext _db = db;

    public async Task<IReadOnlyList<ApiTokenListItemDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var tokens = await _db.ApiTokens.AsNoTracking()
            .OrderByDescending(t => t.CriadoEm)
            .ToListAsync(cancellationToken);

        return [.. tokens.Select(t => new ApiTokenListItemDto(
            t.Id, t.Nome, t.Prefixo, t.Ativo, t.CriadoEm, t.UltimoUsoEm, t.RevogadoEm))];
    }

    public async Task<ApiTokenCriadoDto> CriarAsync(
        CriarApiTokenRequest request, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        var nome = request.Nome?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ValidacaoException("api_token.nome_obrigatorio", "Informe um nome para o token.");
        }

        var (tokenEmClaro, prefixo) = ApiTokenHasher.Gerar();

        var token = new ApiToken
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            TokenHash = ApiTokenHasher.Hash(tokenEmClaro),
            Prefixo = prefixo,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioId,
        };
        _db.ApiTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);

        return new ApiTokenCriadoDto(token.Id, token.Nome, token.Prefixo, tokenEmClaro);
    }

    public async Task RevogarAsync(
        Guid id, Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        var token = await _db.ApiTokens.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("ApiToken", id.ToString());

        if (!token.Ativo) return; // idempotente

        token.Ativo = false;
        token.RevogadoEm = DateTime.UtcNow;
        token.RevogadoPor = usuarioId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
