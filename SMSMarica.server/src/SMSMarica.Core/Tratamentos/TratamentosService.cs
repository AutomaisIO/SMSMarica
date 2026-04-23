using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Tratamentos.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Tratamentos;

public sealed class TratamentosService(SmsMaricaDbContext db) : ITratamentosService
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<TratamentoListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var tratamentos = await _db.Tratamentos.AsNoTracking()
            .OrderByDescending(t => t.CriadoEm)
            .ToListAsync(cancellationToken);
        return [.. tratamentos.Select(TratamentosMapper.ParaListItem)];
    }

    public async Task<TratamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.AsNoTracking()
            .Include(x => x.Periodicidade)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);
        return TratamentosMapper.ParaDto(t);
    }

    public async Task<Guid> CadastrarAsync(CadastrarTratamentoRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _db.Pacientes.AsNoTracking().AnyAsync(p => p.Id == request.PacienteId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Paciente), request.PacienteId);
        }

        if (!await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == request.UnidadeId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Unidade), request.UnidadeId);
        }

        var agora = DateTime.UtcNow;
        var tratamentoId = Guid.CreateVersion7();

        var tratamento = new Tratamento
        {
            Id = tratamentoId,
            PacienteId = request.PacienteId,
            UnidadeId = request.UnidadeId,
            Descricao = request.Descricao.Trim(),
            Ativo = true,
            CriadoEm = agora,
            Periodicidade = new Periodicidade
            {
                Id = Guid.CreateVersion7(),
                TratamentoId = tratamentoId,
                Tipo = request.Periodicidade.Tipo,
                IntervaloDias = request.Periodicidade.IntervaloDias,
                DiasSemanaMascara = request.Periodicidade.DiasSemanaMascara,
                DataInicio = request.Periodicidade.DataInicio,
                QuantidadeSessoes = request.Periodicidade.QuantidadeSessoes,
                CriadoEm = agora,
            },
        };

        _db.Tratamentos.Add(tratamento);
        await _db.SaveChangesAsync(cancellationToken);
        return tratamento.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarTratamentoRequest request, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        if (!t.Ativo)
        {
            throw new ConflitoException("tratamento.encerrado", "Tratamento encerrado não pode ser alterado.");
        }

        t.Descricao = request.Descricao.Trim();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EncerrarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        if (!t.Ativo)
        {
            throw new ConflitoException("tratamento.ja_encerrado", "Tratamento já está encerrado.");
        }

        t.Ativo = false;
        t.EncerradoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
