using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Procedimentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Procedimentos;

public sealed class ProcedimentosSigtapService(SmsMaisDbContext db) : IProcedimentosSigtapService
{
    private readonly SmsMaisDbContext _db = db;

    public async Task<IReadOnlyList<ProcedimentoSigtapDto>> ListarAsync(
        string? busca,
        string? grupo,
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ProcedimentoSigtap> query = _db.ProcedimentosSigtap.AsNoTracking().Where(p => p.Ativo);

        if (!string.IsNullOrWhiteSpace(grupo))
        {
            var g = grupo.Trim();
            query = query.Where(p => p.Grupo == g);
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            var b = busca.Trim();
            // Código sem pontos para comparação: usuário pode digitar "0204030188" ou "02.04.03.018-8".
            var digitos = new string([.. b.Where(char.IsLetterOrDigit)]);
            query = query.Where(p =>
                EF.Functions.ILike(p.Nome, "%" + b + "%")
                || EF.Functions.Like(p.Codigo, b + "%")
                || (digitos.Length > 0 && EF.Functions.Like(p.Codigo.Replace(".", "").Replace("-", ""), digitos + "%")));
        }

        var l = limite is <= 0 or > 200 ? 50 : limite;

        var lista = await query
            .OrderBy(p => p.Codigo)
            .Take(l)
            .ToListAsync(cancellationToken);

        return [.. lista.Select(ParaDto)];
    }

    public async Task<ProcedimentoSigtapDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await _db.ProcedimentosSigtap.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ProcedimentoSigtap), id);
        return ParaDto(p);
    }

    private static ProcedimentoSigtapDto ParaDto(ProcedimentoSigtap p) => new(
        p.Id, p.Codigo, p.Nome, p.Grupo, p.Subgrupo, p.Forma, p.Descricao,
        p.Ativo, p.CompetenciaInicio, p.CompetenciaFim);
}
