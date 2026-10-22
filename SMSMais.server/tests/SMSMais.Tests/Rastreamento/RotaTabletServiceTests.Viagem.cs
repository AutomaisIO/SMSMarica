using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Faturamento;
using SMSMais.Core.Faturamento.Dtos;
using SMSMais.Core.Geo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Core.Translado.Viagem;
using SMSMais.Core.Tratamentos;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Etapa 2: a viagem pelo tablet. O que conta é o transporte feito — o desembarque do paciente no
/// destino marca a viagem como realizada e contabiliza o translado; a ausência na coleta, como não
/// realizada. O painel corrige, e o que ele corrigiu o tablet não muda mais. Pacientes fictícios.
/// </summary>
public partial class RotaTabletServiceTests
{
    private FaturamentoService Faturamento(SmsMaisDbContext db) =>
        new(db, _geo, Substitute.For<IDistanciaService>(), _pacientes, _resolver, NullLogger<FaturamentoService>.Instance);

    private ViagemService Viagem(SmsMaisDbContext db) =>
        new(db, Faturamento(db), _resolver, NullLogger<ViagemService>.Instance);

    private static RegistrarEventoViagemRequest Marca(
        Guid rotaId, TipoEventoViagem tipo, Guid? sessaoId = null, PessoaViagem? pessoa = null, int? ordem = null,
        Guid? motoristaId = null) =>
        new(Guid.NewGuid(), rotaId, tipo, sessaoId, pessoa, ordem, null, motoristaId, DateTime.UtcNow,
            -22.91, -42.82, 8);

    /// <summary>Rota de hoje do veículo: Ana (com acompanhante) e Beatriz para a mesma clínica.</summary>
    private async Task<(Frota F, Guid RotaId, Guid Ana, Guid Beatriz)> ViagemDeHojeAsync(SmsMaisDbContext db)
    {
        var f = await VeiculoComTabletAsync(db);
        var (unidadeId, tipoId) = await DestinoAsync(db);
        var ana = await SessaoAsync(db, Guid.NewGuid(), unidadeId, tipoId, acompanhanteEsperado: true);
        var beatriz = await SessaoAsync(db, Guid.NewGuid(), unidadeId, tipoId, status: StatusSessao.Confirmada);
        var rotaId = await RotaAsync(db, f);
        await AlocarAsync(db, rotaId, ana, f.Assentos[0], TipoAlocacao.Paciente, 1);
        await AlocarAsync(db, rotaId, ana, f.Assentos[3], TipoAlocacao.Acompanhante, 1);
        await AlocarAsync(db, rotaId, beatriz, f.Assentos[1], TipoAlocacao.Paciente, 2);
        return (f, rotaId, ana, beatriz);
    }

    [Fact]
    public async Task Viagem_pelo_tablet_inicia_marca_conta_o_translado_e_conclui()
    {
        await using var db = fixture.CriarDbContext();
        var (f, rotaId, ana, beatriz) = await ViagemDeHojeAsync(db);
        var viagem = Viagem(db);

        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Inicio, motoristaId: f.MotoristaId));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, ana));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, ana, PessoaViagem.Acompanhante, 1));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Ausencia, beatriz));

        var emAndamento = await db.Rotas.AsNoTracking().SingleAsync(r => r.Id == rotaId);
        Assert.Equal(StatusRota.EmAndamento, emAndamento.Status);
        Assert.NotNull(emAndamento.IniciadaEm);
        var sBeatriz = await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == beatriz);
        Assert.Equal(StatusSessao.NaoRealizada, sBeatriz.Status);
        Assert.Equal(ViagemService.MotivoAusencia, sBeatriz.MotivoNaoRealizacao);

        // O tablet reiniciado retoma: cada um com a sua marcação, a coleta da Beatriz resolvida.
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());
        var noMeio = Assert.Single(await Servico(db).RotasDoDiaAsync(f.Tablet));
        var coletaAna = noMeio.Paradas.Single(p => p.Id == $"coleta:{ana}");
        Assert.True(coletaAna.Concluida);
        Assert.Equal(RotaTabletService.Embarcou, coletaAna.Passageiros.Single().Situacao);
        Assert.Equal(RotaTabletService.Embarcou, coletaAna.Passageiros.Single().Acompanhantes.Single().Situacao);
        Assert.Equal(RotaTabletService.Ausente, noMeio.Paradas.Single(p => p.Id == $"coleta:{beatriz}").Passageiros.Single().Situacao);
        Assert.False(noMeio.Paradas.Single(p => p.Tipo == TipoParada.Destino).Concluida);

        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, ana));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Conclusao));

        var sAna = await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == ana);
        Assert.Equal(StatusSessao.Realizada, sAna.Status);
        Assert.NotNull(sAna.HoraSaidaResidencia);
        Assert.NotNull(sAna.HoraChegadaUnidade);
        // Minuto cheio: é o que o campo de hora do painel aceita (HH:mm) e confirma de volta.
        Assert.Equal((0, 0), (sAna.HoraSaidaResidencia!.Value.Second, sAna.HoraSaidaResidencia.Value.Millisecond));
        Assert.Equal(f.MotoristaId, sAna.MotoristaIdaId);
        Assert.Equal(f.Tablet.VeiculoId, sAna.VeiculoIdaId);
        var registro = await db.RegistrosFaturamento.AsNoTracking().SingleAsync(r => r.SessaoId == ana);
        Assert.Equal(StatusFaturamento.Pendente, registro.Status);
        Assert.False(await db.RegistrosFaturamento.AnyAsync(r => r.SessaoId == beatriz));
        Assert.Equal(StatusRota.Concluida, (await db.Rotas.AsNoTracking().SingleAsync(r => r.Id == rotaId)).Status);
        Assert.True((await Servico(db).RotasDoDiaAsync(f.Tablet)).Single().Paradas.All(p => p.Concluida));
    }

    [Fact]
    public async Task Reenvio_da_fila_nao_duplica_e_rota_de_outro_veiculo_e_recusada()
    {
        await using var db = fixture.CriarDbContext();
        var (f, rotaId, ana, _) = await ViagemDeHojeAsync(db);
        var outro = await VeiculoComTabletAsync(db);
        var viagem = Viagem(db);

        var marca = Marca(rotaId, TipoEventoViagem.Embarque, ana);
        var primeiro = await viagem.RegistrarDoTabletAsync(f.Tablet, marca);
        var reenvio = await viagem.RegistrarDoTabletAsync(f.Tablet, marca);

        Assert.Equal(primeiro.Id, reenvio.Id);
        Assert.Equal(1, await db.EventosViagem.CountAsync(e => e.IdLocal == marca.IdLocal));
        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            viagem.RegistrarDoTabletAsync(outro.Tablet, Marca(rotaId, TipoEventoViagem.Inicio)));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, Guid.NewGuid())));
    }

    [Fact]
    public async Task Correcao_do_painel_vale_mais_que_o_tablet_e_acerta_o_faturamento()
    {
        await using var db = fixture.CriarDbContext();
        var (f, rotaId, ana, _) = await ViagemDeHojeAsync(db);
        var viagem = Viagem(db);
        var usuario = Substitute.For<IUsuarioAtualAccessor>();
        usuario.UsuarioId.Returns(Guid.NewGuid());
        var painel = new TratamentosService(db, _resolver, Faturamento(db), _pacientes, usuario,
            NullLogger<TratamentosService>.Instance);
        var tratamentoId = (await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == ana)).TratamentoId;
        ConfirmarSessaoRequest Correcao(bool realizada) =>
            new(realizada, null, f.MotoristaId, f.Tablet.VeiculoId, null, null, null, null, null, null,
                realizada ? null : "Paciente foi de carro próprio", null);

        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, ana));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, ana));
        Assert.Equal(StatusFaturamento.Pendente,
            (await db.RegistrosFaturamento.AsNoTracking().SingleAsync(r => r.SessaoId == ana)).Status);

        // A central corrige: não foi no veículo. Sai dos totais e o tablet não desfaz mais.
        await painel.ConfirmarSessaoAsync(tratamentoId, ana, Correcao(realizada: false));
        var cancelado = await db.RegistrosFaturamento.AsNoTracking().SingleAsync(r => r.SessaoId == ana);
        Assert.Equal(StatusFaturamento.Cancelado, cancelado.Status);
        var resumo = await Faturamento(db).ResumoAsync(DimensaoFaturamento.Paciente, cancelado.Competencia, null, null);
        Assert.DoesNotContain(resumo.Itens, i => i.ChaveId == cancelado.PacienteId.ToString());

        var atrasado = await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, ana));
        Assert.False(atrasado.Aplicado);
        Assert.NotNull(atrasado.MotivoNaoAplicado);
        db.ChangeTracker.Clear();
        Assert.Equal(StatusSessao.NaoRealizada, (await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == ana)).Status);

        // Corrigida de novo para realizada: volta a contar.
        await painel.ConfirmarSessaoAsync(tratamentoId, ana, Correcao(realizada: true));
        Assert.Equal(StatusFaturamento.Pendente,
            (await db.RegistrosFaturamento.AsNoTracking().SingleAsync(r => r.SessaoId == ana)).Status);

        var registros = await viagem.ListarDaRotaAsync(rotaId);
        Assert.Equal(3, registros.Count);
        Assert.Equal([true, true, false], registros.Select(r => r.Aplicado));
    }
}
