using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Geo;
using SMSMais.Core.Translado.Geracao.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Etapa 4 no gerador de verdade: a cadeirante na cadeira pega o assento de cadeirante, os dois
/// acompanhantes dela ganham banco, e o paciente em isolamento vai sozinho. Pacientes fictícios,
/// num dia só deste teste.
/// </summary>
public partial class RotaTabletServiceTests
{
    private static async Task<Veiculo> VanAdaptadaAsync(SmsMaisDbContext db)
    {
        var agora = DateTime.UtcNow;
        var van = new Veiculo
        {
            Id = Guid.NewGuid(), Placa = $"A{Random.Shared.Next(100000, 999999)}", Modelo = "Van adaptada (simulada)",
            Fabricante = "Fiat", Cor = "Branca", Tipo = TipoVeiculo.Van, CriadoEm = agora,
        };
        var fileira = new Fileira { Id = Guid.NewGuid(), VeiculoId = van.Id, Ordem = 1, CriadoEm = agora };
        TipoAssento[] tipos = [TipoAssento.Passageiro, TipoAssento.Cadeirante, TipoAssento.Acompanhante, TipoAssento.Acompanhante];
        db.Veiculos.Add(van);
        db.Fileiras.Add(fileira);
        db.Assentos.AddRange(tipos.Select((t, i) => new Assento
        {
            Id = Guid.NewGuid(), FileiraId = fileira.Id, Numero = i + 1, Tipo = t, CriadoEm = agora,
        }));
        await db.SaveChangesAsync();
        return van;
    }

    [Fact]
    public async Task Gerador_poe_cadeirante_no_assento_dela_reserva_dois_acompanhantes_e_isola_quem_precisa()
    {
        await using var db = fixture.CriarDbContext();
        await VeiculoComTabletAsync(db); // motorista e um carro comum
        await VeiculoComTabletAsync(db);
        await VanAdaptadaAsync(db);
        var (unidadeId, tipoId) = await DestinoAsync(db);
        var dia = new DateOnly(1997, 5, 6);

        var (ana, beto) = (Guid.NewGuid(), Guid.NewGuid());
        var sessaoAna = await SessaoAsync(db, ana, unidadeId, tipoId, MobilidadeTransporte.CadeiranteVeiculoAdaptado, dia: dia);
        var sessaoBeto = await SessaoAsync(db, beto, unidadeId, tipoId, dia: dia);
        var tAna = await db.Tratamentos.SingleAsync(t => t.PacienteId == ana);
        tAna.QuantidadeAcompanhantes = 2;
        var tBeto = await db.Tratamentos.SingleAsync(t => t.PacienteId == beto);
        tBeto.Isolamento = true;
        foreach (var nome in new[] { "Carlos Simulado", "Bia Simulada" })
        {
            var a = new Acompanhante
            {
                Id = Guid.NewGuid(), PacienteId = ana, Cpf = $"{Random.Shared.NextInt64(10000000000, 99999999999)}",
                Nome = nome, DataNascimento = new DateOnly(1980, 1, 1), CriadoEm = DateTime.UtcNow,
            };
            db.Acompanhantes.Add(a);
            db.SessoesAcompanhantes.Add(new SessaoAcompanhante { SessaoId = sessaoAna, AcompanhanteId = a.Id });
        }
        await db.SaveChangesAsync();

        var endereco = new EnderecoDto("24900000", "Rua Simulada", "1", null, "Centro", "Maricá", "RJ", null);
        _pacientes.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(c => PacienteFicticio((Guid)c[0], "Paciente Simulado", endereco));
        _geo.GeocodificarAsync(Arg.Any<Endereco?>(), Arg.Any<CancellationToken>()).Returns(new Coordenada(-22.92, -42.82));

        var resultado = await Gerador(db).GerarAsync(new GerarTransladoRequest(dia, Confirmar: true, UsarIa: false));

        Assert.DoesNotContain(resultado.NaoAlocadas, n => n.SessaoId == sessaoAna || n.SessaoId == sessaoBeto);
        var daAna = await (
            from a in db.Alocacoes.AsNoTracking()
            where a.SessaoId == sessaoAna
            join s in db.Assentos.AsNoTracking() on a.AssentoId equals s.Id
            select new { a.Tipo, a.RotaDiariaId, Assento = s.Tipo }).ToListAsync();
        Assert.Equal(TipoAssento.Cadeirante, daAna.Single(x => x.Tipo == TipoAlocacao.Paciente).Assento);
        Assert.Equal(2, daAna.Count(x => x.Tipo == TipoAlocacao.Acompanhante));

        var rotaDoBeto = await db.Alocacoes.AsNoTracking().Where(a => a.SessaoId == sessaoBeto).Select(a => a.RotaDiariaId).SingleAsync();
        Assert.Equal([sessaoBeto], await db.Alocacoes.AsNoTracking()
            .Where(a => a.RotaDiariaId == rotaDoBeto).Select(a => a.SessaoId).Distinct().ToListAsync());
        Assert.NotEqual(rotaDoBeto, daAna[0].RotaDiariaId);
    }
}
