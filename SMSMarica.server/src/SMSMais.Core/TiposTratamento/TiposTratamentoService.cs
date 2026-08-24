using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.TiposTratamento.Dtos;
using SMSMais.Data;
using EntidadeTipoTratamento = SMSMais.Data.Entities.TipoTratamento;

namespace SMSMais.Core.TiposTratamento;

public sealed class TiposTratamentoService(SmsMaisDbContext db) : ITiposTratamentoService
{
    private readonly SmsMaisDbContext _db = db;

    public async Task<IReadOnlyList<TipoTratamentoListItemDto>> ListarAsync(bool somenteAtivos, CancellationToken cancellationToken = default)
    {
        var query = _db.TiposTratamento.AsNoTracking();
        if (somenteAtivos) query = query.Where(t => t.Ativo);
        var lista = await query
            .OrderBy(t => t.Nome)
            .Select(t => new TipoTratamentoListItemDto(t.Id, t.Nome, t.Codigo, t.Ativo))
            .ToListAsync(cancellationToken);
        return lista;
    }

    public async Task<TipoTratamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.TiposTratamento.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(EntidadeTipoTratamento), id);
        return new TipoTratamentoDto(t.Id, t.Nome, t.Codigo, t.Ativo, t.CriadoEm);
    }

    public async Task<Guid> CadastrarAsync(CadastrarTipoTratamentoRequest request, CancellationToken cancellationToken = default)
    {
        var codigo = NormalizarCodigo(request.Codigo);
        if (await _db.TiposTratamento.AsNoTracking().AnyAsync(t => t.Codigo == codigo, cancellationToken))
        {
            throw new ConflitoException("tipo_tratamento.codigo_duplicado", "Já existe tipo de tratamento com este código.");
        }

        var t = new EntidadeTipoTratamento
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Codigo = codigo,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        _db.TiposTratamento.Add(t);
        await _db.SaveChangesAsync(cancellationToken);
        return t.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarTipoTratamentoRequest request, CancellationToken cancellationToken = default)
    {
        var t = await _db.TiposTratamento.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(EntidadeTipoTratamento), id);

        var codigo = NormalizarCodigo(request.Codigo);
        if (!string.Equals(t.Codigo, codigo, StringComparison.Ordinal) &&
            await _db.TiposTratamento.AsNoTracking().AnyAsync(x => x.Codigo == codigo && x.Id != id, cancellationToken))
        {
            throw new ConflitoException("tipo_tratamento.codigo_duplicado", "Já existe tipo de tratamento com este código.");
        }

        t.Nome = request.Nome.Trim();
        t.Codigo = codigo;
        t.Ativo = request.Ativo;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.TiposTratamento.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(EntidadeTipoTratamento), id);
        if (!t.Ativo)
        {
            throw new ConflitoException("tipo_tratamento.ja_inativo", "Tipo de tratamento já está inativo.");
        }
        t.Ativo = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizarCodigo(string codigo) =>
        codigo.Trim().ToLowerInvariant().Replace(' ', '_');
}
