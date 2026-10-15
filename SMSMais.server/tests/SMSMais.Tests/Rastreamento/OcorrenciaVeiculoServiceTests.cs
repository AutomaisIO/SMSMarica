using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Rastreamento;
using SMSMais.Core.Rastreamento.Ocorrencias;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Botão REPORT do tablet do veículo: a ocorrência entra uma vez só (a fila offline do tablet reenvia),
/// "Outro" exige descrição, posição inválida não derruba o registro, a central é avisada e anda a
/// situação Aberta → EmAtendimento → Encerrada.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class OcorrenciaVeiculoServiceTests(PostgresFixture fixture)
{
    private static OcorrenciaVeiculoService Servico(
        SmsMaisDbContext db, IRastreamentoNotificador notificador, Guid? usuarioId = null) =>
        new(db, new UsuarioAtualAccessorFake(usuarioId), notificador, NullLogger<OcorrenciaVeiculoService>.Instance);

    private static async Task<DispositivoVeiculo> TabletVinculadoAsync(SmsMaisDbContext db)
    {
        var agora = DateTime.UtcNow;
        var veiculo = new Veiculo
        {
            Id = Guid.NewGuid(),
            Placa = $"T{Random.Shared.Next(100000, 999999)}",
            Modelo = "Spin",
            Fabricante = "Chevrolet",
            Cor = "Branca",
            CriadoEm = agora,
        };
        var tablet = new DispositivoVeiculo
        {
            Id = Guid.NewGuid(),
            VeiculoId = veiculo.Id,
            TokenHash = Guid.NewGuid().ToString("N"),
            AtivadoEm = agora,
            CriadoEm = agora,
        };
        db.Veiculos.Add(veiculo);
        db.DispositivosVeiculo.Add(tablet);
        await db.SaveChangesAsync();
        return tablet;
    }

    private static RegistrarOcorrenciaRequest Report(
        TipoOcorrenciaVeiculo tipo = TipoOcorrenciaVeiculo.Acidente,
        string? descricao = null,
        Guid? idLocal = null,
        double? latitude = -22.91,
        double? longitude = -42.82) =>
        new(idLocal ?? Guid.NewGuid(), tipo, descricao, latitude, longitude, 8, null, DateTime.UtcNow);

    [Fact]
    public async Task Registra_aberta_com_posicao_e_avisa_a_central()
    {
        await using var db = fixture.CriarDbContext();
        var tablet = await TabletVinculadoAsync(db);
        var notificador = Substitute.For<IRastreamentoNotificador>();

        var r = await Servico(db, notificador).RegistrarAsync(tablet, Report(descricao: "  batida leve  "));

        var salva = await db.OcorrenciasVeiculo.AsNoTracking().SingleAsync(o => o.Id == r.Id);
        Assert.Equal(SituacaoOcorrenciaVeiculo.Aberta, salva.Situacao);
        Assert.Equal(tablet.VeiculoId, salva.VeiculoId);
        Assert.Equal("batida leve", salva.Descricao);
        Assert.Equal(-22.91, salva.Latitude);
        await notificador.Received(1).OcorrenciaRegistradaAsync(
            r.Id, tablet.VeiculoId, Arg.Any<string>(), TipoOcorrenciaVeiculo.Acidente, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reenvio_com_o_mesmo_id_local_nao_duplica_nem_avisa_de_novo()
    {
        await using var db = fixture.CriarDbContext();
        var tablet = await TabletVinculadoAsync(db);
        var notificador = Substitute.For<IRastreamentoNotificador>();
        var idLocal = Guid.NewGuid();

        var primeira = await Servico(db, notificador).RegistrarAsync(tablet, Report(idLocal: idLocal));
        var reenvio = await Servico(db, notificador).RegistrarAsync(tablet, Report(idLocal: idLocal));

        Assert.Equal(primeira.Id, reenvio.Id);
        Assert.Equal(1, await db.OcorrenciasVeiculo.CountAsync(o => o.IdLocal == idLocal));
        await notificador.ReceivedWithAnyArgs(1).OcorrenciaRegistradaAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task Outro_sem_descricao_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var tablet = await TabletVinculadoAsync(db);

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db, Substitute.For<IRastreamentoNotificador>())
            .RegistrarAsync(tablet, Report(TipoOcorrenciaVeiculo.Outro, descricao: "   ")));
    }

    [Fact]
    public async Task Gps_sem_fix_registra_sem_posicao()
    {
        await using var db = fixture.CriarDbContext();
        var tablet = await TabletVinculadoAsync(db);

        var r = await Servico(db, Substitute.For<IRastreamentoNotificador>())
            .RegistrarAsync(tablet, Report(latitude: 0, longitude: 0));

        var salva = await db.OcorrenciasVeiculo.AsNoTracking().SingleAsync(o => o.Id == r.Id);
        Assert.Null(salva.Latitude);
        Assert.Null(salva.Longitude);
    }

    [Fact]
    public async Task Falha_no_aviso_em_tempo_real_nao_perde_a_ocorrencia()
    {
        await using var db = fixture.CriarDbContext();
        var tablet = await TabletVinculadoAsync(db);
        var notificador = Substitute.For<IRastreamentoNotificador>();
        notificador.OcorrenciaRegistradaAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("hub fora")));

        var r = await Servico(db, notificador).RegistrarAsync(tablet, Report());

        Assert.True(await db.OcorrenciasVeiculo.AnyAsync(o => o.Id == r.Id));
    }

    [Fact]
    public async Task Central_atende_e_encerra_com_quem_e_quando()
    {
        await using var db = fixture.CriarDbContext();
        var tablet = await TabletVinculadoAsync(db);
        var operador = Guid.NewGuid();
        var notificador = Substitute.For<IRastreamentoNotificador>();
        var servico = Servico(db, notificador, operador);
        var r = await servico.RegistrarAsync(tablet, Report(TipoOcorrenciaVeiculo.PaneVeiculo));

        var atendida = await servico.AtenderAsync(r.Id);
        Assert.Equal(SituacaoOcorrenciaVeiculo.EmAtendimento, atendida.Situacao);
        Assert.NotNull(atendida.AtendidaEm);

        var encerrada = await servico.EncerrarAsync(r.Id, new EncerrarOcorrenciaRequest("Guincho a caminho"));
        Assert.Equal(SituacaoOcorrenciaVeiculo.Encerrada, encerrada.Situacao);
        Assert.Equal("Guincho a caminho", encerrada.ObservacaoCentral);

        var salva = await db.OcorrenciasVeiculo.AsNoTracking().SingleAsync(o => o.Id == r.Id);
        Assert.Equal(operador, salva.AtendidaPor);
        Assert.Equal(operador, salva.EncerradaPor);
        await notificador.Received(2).OcorrenciaAtualizadaAsync(r.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Encerrada_nao_volta_a_ser_atendida_nem_encerrada()
    {
        await using var db = fixture.CriarDbContext();
        var tablet = await TabletVinculadoAsync(db);
        var servico = Servico(db, Substitute.For<IRastreamentoNotificador>());
        var r = await servico.RegistrarAsync(tablet, Report());
        await servico.EncerrarAsync(r.Id, new EncerrarOcorrenciaRequest(null));

        await Assert.ThrowsAsync<ConflitoException>(() => servico.AtenderAsync(r.Id));
        await Assert.ThrowsAsync<ConflitoException>(() =>
            servico.EncerrarAsync(r.Id, new EncerrarOcorrenciaRequest(null)));
    }

    [Fact]
    public async Task Lista_separa_em_aberto_de_encerradas()
    {
        await using var db = fixture.CriarDbContext();
        var tablet = await TabletVinculadoAsync(db);
        var servico = Servico(db, Substitute.For<IRastreamentoNotificador>());
        var aberta = await servico.RegistrarAsync(tablet, Report());
        var fechada = await servico.RegistrarAsync(tablet, Report());
        await servico.EncerrarAsync(fechada.Id, new EncerrarOcorrenciaRequest(null));

        var emAberto = await servico.ListarAsync(encerradas: false);
        var encerradas = await servico.ListarAsync(encerradas: true);

        Assert.Contains(emAberto, o => o.Id == aberta.Id);
        Assert.DoesNotContain(emAberto, o => o.Id == fechada.Id);
        Assert.Contains(encerradas, o => o.Id == fechada.Id);
    }
}
