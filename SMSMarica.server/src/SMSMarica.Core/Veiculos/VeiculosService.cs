using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Veiculos.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Veiculos;

public sealed class VeiculosService(SmsMaricaDbContext db) : IVeiculosService
{
    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<VeiculoListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var veiculos = await _db.Veiculos.AsNoTracking()
            .OrderBy(v => v.Placa)
            .ToListAsync(cancellationToken);
        return [.. veiculos.Select(VeiculosMapper.ParaListItem)];
    }

    public async Task<VeiculoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var v = await _db.Veiculos.AsNoTracking()
            .Include(x => x.Fileiras.OrderBy(f => f.Ordem))
                .ThenInclude(f => f.Assentos.OrderBy(a => a.Numero))
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Veiculo), id);
        return VeiculosMapper.ParaDto(v);
    }

    public async Task<Guid> CadastrarAsync(CadastrarVeiculoRequest request, CancellationToken cancellationToken = default)
    {
        var placa = request.Placa.Trim().ToUpperInvariant();

        if (await _db.Veiculos.AsNoTracking().AnyAsync(x => x.Placa == placa, cancellationToken))
        {
            throw new ConflitoException("veiculo.placa_duplicada", "Já existe veículo com esta placa.");
        }

        var agora = DateTime.UtcNow;
        var v = new Veiculo
        {
            Id = Guid.CreateVersion7(),
            Placa = placa,
            Modelo = request.Modelo.Trim(),
            Fabricante = request.Fabricante.Trim(),
            Cor = request.Cor.Trim(),
            Tipo = request.Tipo,
            Ativo = true,
            CriadoEm = agora,
            Fileiras = [.. request.Fileiras
                .OrderBy(f => f.Ordem)
                .Select(f => new Fileira
                {
                    Id = Guid.CreateVersion7(),
                    Ordem = f.Ordem,
                    QuantidadeAssentos = f.Assentos.Count,
                    CriadoEm = agora,
                    Assentos = [.. f.Assentos
                        .OrderBy(a => a.Numero)
                        .Select(a => new Assento
                        {
                            Id = Guid.CreateVersion7(),
                            Numero = a.Numero,
                            Tipo = a.Tipo,
                            CriadoEm = agora,
                        })],
                })],
        };

        _db.Veiculos.Add(v);
        await _db.SaveChangesAsync(cancellationToken);
        return v.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarVeiculoRequest request, CancellationToken cancellationToken = default)
    {
        var v = await _db.Veiculos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Veiculo), id);

        var placa = request.Placa.Trim().ToUpperInvariant();
        if (placa != v.Placa &&
            await _db.Veiculos.AsNoTracking().AnyAsync(x => x.Placa == placa && x.Id != id, cancellationToken))
        {
            throw new ConflitoException("veiculo.placa_duplicada", "Já existe veículo com esta placa.");
        }

        v.Placa = placa;
        v.Modelo = request.Modelo.Trim();
        v.Fabricante = request.Fabricante.Trim();
        v.Cor = request.Cor.Trim();
        v.Tipo = request.Tipo;
        v.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var v = await _db.Veiculos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Veiculo), id);

        if (!v.Ativo)
        {
            throw new ConflitoException("veiculo.ja_inativo", "Veículo já está inativo.");
        }

        v.Ativo = false;
        v.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AtualizarLayoutAsync(
        Guid veiculoId,
        AtualizarLayoutVeiculoRequest request,
        CancellationToken cancellationToken = default)
    {
        var veiculo = await _db.Veiculos
            .Include(v => v.Fileiras)
                .ThenInclude(f => f.Assentos)
            .FirstOrDefaultAsync(v => v.Id == veiculoId && v.Ativo, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Veiculo), veiculoId);

        var agora = DateTime.UtcNow;
        var ordensNovas = request.Fileiras.Select(f => f.Ordem).ToHashSet();

        // Fileiras removidas do layout: soft-delete em todos os assentos
        foreach (var fileira in veiculo.Fileiras.Where(f => !ordensNovas.Contains(f.Ordem)))
            foreach (var assento in fileira.Assentos.Where(a => !a.Excluido))
                assento.Excluido = true;

        foreach (var fileiraReq in request.Fileiras)
        {
            var fileira = veiculo.Fileiras.FirstOrDefault(f => f.Ordem == fileiraReq.Ordem);
            if (fileira is null)
            {
                fileira = new Fileira
                {
                    Id = Guid.CreateVersion7(),
                    VeiculoId = veiculoId,
                    Ordem = fileiraReq.Ordem,
                    QuantidadeAssentos = fileiraReq.Assentos.Count,
                    CriadoEm = agora,
                    Assentos = [],
                };
                _db.Fileiras.Add(fileira);
            }
            else
            {
                fileira.QuantidadeAssentos = fileiraReq.Assentos.Count;
            }

            var numerosNovos = fileiraReq.Assentos.Select(a => a.Numero).ToHashSet();

            // Assentos removidos desta fileira: soft-delete
            foreach (var assento in fileira.Assentos.Where(a => !a.Excluido && !numerosNovos.Contains(a.Numero)))
                assento.Excluido = true;

            // Atualizar existentes ou inserir novos
            foreach (var assentoReq in fileiraReq.Assentos)
            {
                var assento = fileira.Assentos.FirstOrDefault(a => a.Numero == assentoReq.Numero && !a.Excluido);
                if (assento is null)
                {
                    fileira.Assentos.Add(new Assento
                    {
                        Id = Guid.CreateVersion7(),
                        FileiraId = fileira.Id,
                        Numero = assentoReq.Numero,
                        Tipo = assentoReq.Tipo,
                        Bloqueado = assentoReq.Bloqueado,
                        CriadoEm = agora,
                    });
                }
                else
                {
                    assento.Tipo = assentoReq.Tipo;
                    assento.Bloqueado = assentoReq.Bloqueado;
                }
            }
        }

        veiculo.AtualizadoEm = agora;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<FileiraDto> AdicionarFileiraAsync(
        Guid veiculoId,
        AdicionarFileiraRequest request,
        CancellationToken cancellationToken = default)
    {
        var v = await _db.Veiculos
            .Include(x => x.Fileiras)
            .FirstOrDefaultAsync(x => x.Id == veiculoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Veiculo), veiculoId);

        if (v.Fileiras.Any(f => f.Ordem == request.Ordem))
        {
            throw new ConflitoException(
                "fileira.ordem_duplicada",
                $"Veículo já tem fileira com ordem {request.Ordem}.");
        }

        var agora = DateTime.UtcNow;
        var fileira = new Fileira
        {
            Id = Guid.CreateVersion7(),
            VeiculoId = veiculoId,
            Ordem = request.Ordem,
            QuantidadeAssentos = request.QuantidadeAssentos,
            CriadoEm = agora,
            Assentos = [.. Enumerable.Range(1, request.QuantidadeAssentos)
                .Select(n => new Assento
                {
                    Id = Guid.CreateVersion7(),
                    Numero = n,
                    Tipo = TipoAssento.Passageiro,
                    CriadoEm = agora,
                })],
        };

        v.Fileiras.Add(fileira);
        await _db.SaveChangesAsync(cancellationToken);

        return VeiculosMapper.ParaDto(fileira);
    }

    public async Task RemoverFileiraAsync(Guid veiculoId, Guid fileiraId, CancellationToken cancellationToken = default)
    {
        var fileira = await _db.Fileiras
            .FirstOrDefaultAsync(f => f.Id == fileiraId && f.VeiculoId == veiculoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Fileira), fileiraId);

        _db.Fileiras.Remove(fileira);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
