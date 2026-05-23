using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Core.Perfis.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Perfis;

public sealed class PerfisService(SmsMaricaDbContext db) : IPerfisService
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<PerfilListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var lista = await _db.Perfis.AsNoTracking()
            .OrderBy(p => p.Nome)
            .Select(p => new PerfilListItemDto(
                p.Id, p.Nome, p.Descricao, p.Ativo,
                p.Permissoes.Count))
            .ToListAsync(cancellationToken);
        return lista;
    }

    public async Task<PerfilDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var perfil = await _db.Perfis.AsNoTracking()
            .Include(p => p.Permissoes)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Perfil), id);

        var permissoes = perfil.Permissoes
            .OrderBy(p => p.Modulo)
            .Select(p => new PermissaoModuloDto(p.Modulo, p.Acoes))
            .ToList();

        return new PerfilDto(perfil.Id, perfil.Nome, perfil.Descricao, perfil.Ativo, perfil.CriadoEm, permissoes);
    }

    public async Task<Guid> CadastrarAsync(CadastrarPerfilRequest request, CancellationToken cancellationToken = default)
    {
        var nome = request.Nome.Trim();
        if (await _db.Perfis.AsNoTracking().AnyAsync(p => p.Nome == nome, cancellationToken))
        {
            throw new ConflitoException("perfil.nome_duplicado", "Já existe perfil com este nome.");
        }

        var perfil = new Perfil
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            Permissoes = [.. ConsolidarPermissoes(request.Permissoes).Select(p => new PermissaoPerfil { Modulo = p.Modulo, Acoes = p.Acoes })],
        };

        _db.Perfis.Add(perfil);
        await _db.SaveChangesAsync(cancellationToken);
        return perfil.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarPerfilRequest request, CancellationToken cancellationToken = default)
    {
        var perfil = await _db.Perfis
            .Include(p => p.Permissoes)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Perfil), id);

        var nome = request.Nome.Trim();
        if (!string.Equals(perfil.Nome, nome, StringComparison.Ordinal) &&
            await _db.Perfis.AsNoTracking().AnyAsync(p => p.Nome == nome && p.Id != id, cancellationToken))
        {
            throw new ConflitoException("perfil.nome_duplicado", "Já existe perfil com este nome.");
        }

        perfil.Nome = nome;
        perfil.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        perfil.Ativo = request.Ativo;

        // Sincroniza permissões: substitui o conjunto inteiro.
        perfil.Permissoes.Clear();
        foreach (var p in ConsolidarPermissoes(request.Permissoes))
        {
            perfil.Permissoes.Add(new PermissaoPerfil { PerfilId = id, Modulo = p.Modulo, Acoes = p.Acoes });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var perfil = await _db.Perfis.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Perfil), id);

        if (!perfil.Ativo)
        {
            throw new ConflitoException("perfil.ja_inativo", "Perfil já está inativo.");
        }

        perfil.Ativo = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Agrupa as permissões por módulo, fazendo OR das ações; descarta Nenhuma.</summary>
    private static IEnumerable<PermissaoModuloDto> ConsolidarPermissoes(IReadOnlyList<PermissaoModuloDto>? entradas) =>
        (entradas ?? [])
            .Where(p => p.Acoes != AcoesPermissao.Nenhuma)
            .GroupBy(p => p.Modulo)
            .Select(g => new PermissaoModuloDto(g.Key, g.Aggregate(AcoesPermissao.Nenhuma, (acc, x) => acc | x.Acoes)));
}
