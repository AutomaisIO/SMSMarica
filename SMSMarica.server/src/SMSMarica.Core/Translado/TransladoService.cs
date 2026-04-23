using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Translado.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Translado;

public sealed class TransladoService(SmsMaricaDbContext db) : ITransladoService
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<RotaDiariaListItemDto>> ListarAsync(DateOnly? data, CancellationToken cancellationToken = default)
    {
        var query = _db.Rotas.AsNoTracking().AsQueryable();
        if (data is not null)
        {
            query = query.Where(r => r.Data == data.Value);
        }

        var rotas = await query
            .OrderByDescending(r => r.Data)
            .ThenBy(r => r.CriadoEm)
            .ToListAsync(cancellationToken);

        return [.. rotas.Select(TransladoMapper.ParaListItem)];
    }

    public async Task<RotaDiariaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);
        return TransladoMapper.ParaDto(r);
    }

    public async Task<Guid> CadastrarAsync(CadastrarRotaRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _db.Veiculos.AsNoTracking().AnyAsync(v => v.Id == request.VeiculoId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Veiculo), request.VeiculoId);
        }

        if (!await _db.Motoristas.AsNoTracking().AnyAsync(m => m.Id == request.MotoristaId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Motorista), request.MotoristaId);
        }

        var r = new RotaDiaria
        {
            Id = Guid.CreateVersion7(),
            Data = request.Data,
            VeiculoId = request.VeiculoId,
            MotoristaId = request.MotoristaId,
            Status = StatusRota.Planejada,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Rotas.Add(r);
        await _db.SaveChangesAsync(cancellationToken);
        return r.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarRotaRequest request, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        if (r.Status != StatusRota.Planejada)
        {
            throw new ConflitoException(
                "rota.nao_editavel",
                "Só é possível alterar rota em estado Planejada.");
        }

        r.VeiculoId = request.VeiculoId;
        r.MotoristaId = request.MotoristaId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task IniciarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        if (r.Status != StatusRota.Planejada)
        {
            throw new ConflitoException("rota.estado_invalido", $"Não é possível iniciar rota no estado {r.Status}.");
        }

        r.Status = StatusRota.EmAndamento;
        r.IniciadaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ConcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        if (r.Status != StatusRota.EmAndamento)
        {
            throw new ConflitoException("rota.estado_invalido", $"Não é possível concluir rota no estado {r.Status}.");
        }

        r.Status = StatusRota.Concluida;
        r.ConcluidaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        if (r.Status is StatusRota.Concluida or StatusRota.Cancelada)
        {
            throw new ConflitoException("rota.estado_invalido", $"Rota já está em estado {r.Status}.");
        }

        r.Status = StatusRota.Cancelada;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
