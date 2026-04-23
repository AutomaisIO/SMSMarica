using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Pacientes;

public sealed class PacientesService(SmsMaricaDbContext db) : IPacientesService
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<PacienteListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var pacientes = await _db.Pacientes
            .AsNoTracking()
            .OrderBy(p => p.NomeCompleto)
            .ToListAsync(cancellationToken);

        return [.. pacientes.Select(PacientesMapper.ParaListItem)];
    }

    public async Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        return PacientesMapper.ParaDto(paciente);
    }

    public async Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = NormalizarDigitos(request.Cpf);

        var existeCpf = await _db.Pacientes
            .AsNoTracking()
            .AnyAsync(p => p.Cpf == cpfNormalizado, cancellationToken);
        if (existeCpf)
        {
            throw new ConflitoException("paciente.cpf_duplicado", "Já existe paciente com este CPF.");
        }

        var paciente = new Paciente
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Cpf = cpfNormalizado,
            Cns = string.IsNullOrWhiteSpace(request.Cns) ? null : NormalizarDigitos(request.Cns),
            GpsResidencia = new Gps(request.Latitude, request.Longitude),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Pacientes.Add(paciente);
        await _db.SaveChangesAsync(cancellationToken);

        return paciente.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        paciente.NomeCompleto = request.NomeCompleto.Trim();
        paciente.Cns = string.IsNullOrWhiteSpace(request.Cns) ? null : NormalizarDigitos(request.Cns);
        paciente.GpsResidencia = new Gps(request.Latitude, request.Longitude);
        paciente.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await _db.Pacientes
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Paciente), id);

        if (!paciente.Ativo)
        {
            throw new ConflitoException("paciente.ja_inativo", "Paciente já está inativo.");
        }

        paciente.Ativo = false;
        paciente.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizarDigitos(string valor) =>
        new([.. valor.Where(char.IsDigit)]);
}
