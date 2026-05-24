using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.LaudoTemplates.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.LaudoTemplates;

public sealed class LaudoTemplatesService(SmsMaricaDbContext db, IHtmlSanitizer sanitizer) : ILaudoTemplatesService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IHtmlSanitizer _sanitizer = sanitizer;

    public async Task<IReadOnlyList<LaudoTemplateListItemDto>> ListarAsync(
        string? categoria,
        bool incluirInativos,
        CancellationToken cancellationToken = default)
    {
        IQueryable<LaudoTemplate> query = _db.LaudoTemplates.AsNoTracking();

        if (!incluirInativos)
        {
            query = query.Where(t => t.Ativo);
        }

        if (!string.IsNullOrWhiteSpace(categoria))
        {
            var c = categoria.Trim();
            query = query.Where(t => t.Categoria == c);
        }

        var lista = await query
            .OrderBy(t => t.Categoria)
            .ThenBy(t => t.Nome)
            .ToListAsync(cancellationToken);

        return [.. lista.Select(LaudoTemplatesMapper.ParaListItem)];
    }

    public async Task<LaudoTemplateDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.LaudoTemplates
            .AsNoTracking()
            .Include(x => x.CriadoPorUsuario)
            .Include(x => x.AtualizadoPorUsuario)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(LaudoTemplate), id);

        return LaudoTemplatesMapper.ParaDto(t);
    }

    public async Task<Guid> CadastrarAsync(
        Guid usuarioId,
        CadastrarLaudoTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var nome = request.Nome.Trim();
        var categoria = request.Categoria.Trim();

        if (await _db.LaudoTemplates.AsNoTracking().AnyAsync(t => t.Nome == nome && t.Ativo, cancellationToken))
        {
            throw new ConflitoException("laudoTemplate.nome_duplicado", $"Já existe template ativo com o nome '{nome}'.");
        }

        var agora = DateTime.UtcNow;
        var template = new LaudoTemplate
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            Categoria = categoria,
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            ConteudoJson = string.IsNullOrWhiteSpace(request.ConteudoJson) ? "{}" : request.ConteudoJson,
            ConteudoHtml = _sanitizer.Sanitize(request.ConteudoHtml ?? string.Empty),
            CriadoPorUsuarioId = usuarioId,
            CriadoEm = agora,
            Ativo = true,
        };

        _db.LaudoTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);
        return template.Id;
    }

    public async Task AtualizarAsync(
        Guid id,
        Guid usuarioId,
        AtualizarLaudoTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var t = await _db.LaudoTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(LaudoTemplate), id);

        var nome = request.Nome.Trim();
        if (nome != t.Nome &&
            await _db.LaudoTemplates.AsNoTracking().AnyAsync(x => x.Nome == nome && x.Ativo && x.Id != id, cancellationToken))
        {
            throw new ConflitoException("laudoTemplate.nome_duplicado", $"Já existe template ativo com o nome '{nome}'.");
        }

        t.Nome = nome;
        t.Categoria = request.Categoria.Trim();
        t.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        t.ConteudoJson = string.IsNullOrWhiteSpace(request.ConteudoJson) ? "{}" : request.ConteudoJson;
        t.ConteudoHtml = _sanitizer.Sanitize(request.ConteudoHtml ?? string.Empty);
        t.AtualizadoPorUsuarioId = usuarioId;
        t.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.LaudoTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(LaudoTemplate), id);

        if (!t.Ativo)
        {
            throw new ConflitoException("laudoTemplate.ja_inativo", "Template já está inativo.");
        }

        t.Ativo = false;
        t.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.LaudoTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(LaudoTemplate), id);

        if (t.Ativo)
        {
            throw new ConflitoException("laudoTemplate.ja_ativo", "Template já está ativo.");
        }

        // Caso outro template com o mesmo nome tenha sido criado enquanto este estava inativo.
        if (await _db.LaudoTemplates.AsNoTracking().AnyAsync(x => x.Nome == t.Nome && x.Ativo && x.Id != id, cancellationToken))
        {
            throw new ConflitoException(
                "laudoTemplate.nome_em_uso",
                $"Não dá para reativar: existe outro template ativo com o nome '{t.Nome}'.");
        }

        t.Ativo = true;
        t.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
