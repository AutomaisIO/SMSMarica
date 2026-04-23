using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Motoristas.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Motoristas;

public sealed class MotoristasService(SmsMaricaDbContext db) : IMotoristasService
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<MotoristaListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var motoristas = await _db.Motoristas.AsNoTracking()
            .OrderBy(m => m.NomeCompleto)
            .ToListAsync(cancellationToken);
        return [.. motoristas.Select(MotoristasMapper.ParaListItem)];
    }

    public async Task<MotoristaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _db.Motoristas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);
        return MotoristasMapper.ParaDto(m);
    }

    public async Task<Guid> CadastrarAsync(CadastrarMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = NormalizarDigitos(request.Cpf);

        if (await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cpf == cpf, cancellationToken))
        {
            throw new ConflitoException("motorista.cpf_duplicado", "Já existe motorista com este CPF.");
        }

        var cnh = request.Cnh.Trim();
        if (await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        var m = new Motorista
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Cpf = cpf,
            Cnh = cnh,
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Motoristas.Add(m);
        await _db.SaveChangesAsync(cancellationToken);
        return m.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        var m = await _db.Motoristas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);

        var cnh = request.Cnh.Trim();
        if (cnh != m.Cnh &&
            await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh && x.Id != id, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        m.NomeCompleto = request.NomeCompleto.Trim();
        m.Cnh = cnh;
        m.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        m.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _db.Motoristas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);

        if (!m.Ativo)
        {
            throw new ConflitoException("motorista.ja_inativo", "Motorista já está inativo.");
        }

        m.Ativo = false;
        m.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizarDigitos(string valor) =>
        new([.. valor.Where(char.IsDigit)]);
}
