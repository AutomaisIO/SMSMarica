using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Rastreamento;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Core.Rastreamento.Dtos;
using SMSMais.Core.Translado.Viagem;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Etapa 3: a volta depende do destino (decisão de 16/10). Fora do município a van espera e traz de volta
/// quem levou; em Maricá ela não espera — quem desceu entra na fila de "aguardando retorno", com a hora
/// em que deve estar pronto (chegada + tempo médio), e a central puxa para uma rota. Pacientes fictícios.
/// </summary>
public partial class RotaTabletServiceTests
{
    private RastreamentoService Rastreamento(SmsMaisDbContext db) =>
        new(db, Substitute.For<IRastreamentoNotificador>(), _resolver);

    private async Task MarcarNaVoltaAsync(ViagemService viagem, Frota f, Guid rotaId, TipoEventoViagem tipo, Guid sessao) =>
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, tipo, sessao, motoristaId: f.MotoristaId) with { Volta = true });

    [Fact]
    public async Task Fora_do_municipio_a_van_espera_e_traz_de_volta_quem_levou()
    {
        await using var db = fixture.CriarDbContext();
        var (f, rotaId, ana, beatriz) = await ViagemDeHojeAsync(db); // unidade externa (fora do município)
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());
        var viagem = Viagem(db);

        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, ana));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Ausencia, beatriz));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, ana));

        var rota = Assert.Single(await Servico(db).RotasDoDiaAsync(f.Tablet));
        var volta = rota.Paradas.Where(p => p.Trecho == RotaTabletService.Volta).ToList();
        var chegada = (await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == ana)).HoraChegadaUnidade!.Value;
        Assert.Collection(volta,
            retorno =>
            {
                Assert.Equal(TipoParada.Coleta, retorno.Tipo);
                Assert.StartsWith("retorno:", retorno.Id);
                Assert.Contains("espera", retorno.Observacoes);
                Assert.Equal(chegada.AddMinutes(240), retorno.HorarioPrevisto); // tempo médio da Hemodiálise
                Assert.Equal([ana], retorno.Passageiros.Select(p => p.SessaoId)); // quem faltou na ida não volta
            },
            casa =>
            {
                Assert.Equal(TipoParada.Destino, casa.Tipo);
                Assert.Equal($"casa:{ana}", casa.Id);
            });

        await MarcarNaVoltaAsync(viagem, f, rotaId, TipoEventoViagem.Embarque, ana);
        await MarcarNaVoltaAsync(viagem, f, rotaId, TipoEventoViagem.Desembarque, ana);

        var sAna = await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == ana);
        Assert.Equal(StatusSessao.Realizada, sAna.Status);
        Assert.NotNull(sAna.HoraSaidaUnidade);
        Assert.NotNull(sAna.HoraChegadaResidencia);
        Assert.Equal(f.MotoristaId, sAna.MotoristaVoltaId);
        Assert.Equal(f.Tablet.VeiculoId, sAna.VeiculoVoltaId);
        var depois = Assert.Single(await Servico(db).RotasDoDiaAsync(f.Tablet));
        Assert.True(depois.Paradas.All(p => p.Concluida));
    }

    [Fact]
    public async Task Em_Marica_quem_desceu_aguarda_retorno_e_a_central_puxa_para_outra_rota()
    {
        await using var db = fixture.CriarDbContext();
        var (f, rotaId, ana, _) = await ViagemDeHojeAsync(db);
        var unidade = await (from s in db.Sessoes where s.Id == ana
                             join t in db.Tratamentos on s.TratamentoId equals t.Id
                             join u in db.UnidadesAtendimento on t.UnidadeAtendimentoId equals u.Id
                             select u).SingleAsync();
        unidade.Externa = false; // dentro de Maricá: a van não espera
        await db.SaveChangesAsync();
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());
        var viagem = Viagem(db);
        var rastreamento = Rastreamento(db);

        await Assert.ThrowsAsync<ConflitoException>(() => rastreamento.PuxarAsync(new PuxarPacienteRequest(rotaId, ana)));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, ana));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, ana));

        Assert.DoesNotContain((await Servico(db).RotasDoDiaAsync(f.Tablet)).Single().Paradas,
            p => p.Trecho == RotaTabletService.Volta);
        var naFila = (await rastreamento.ListarAguardandoAsync(null)).Single(x => x.SessaoId == ana);
        Assert.False(naFila.ForaDoMunicipio);
        var chegada = (await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == ana)).HoraChegadaUnidade!.Value;
        Assert.Equal(chegada.AddMinutes(240), naFila.ProntoPrevisto);

        // A central puxa para a rota de outro veículo, que passa a ter a volta da Ana.
        var outra = await VeiculoComTabletAsync(db);
        var rotaVolta = await RotaAsync(db, outra);
        await rastreamento.PuxarAsync(new PuxarPacienteRequest(rotaVolta, ana));

        Assert.DoesNotContain(await rastreamento.ListarAguardandoAsync(null), x => x.SessaoId == ana);
        var daOutra = Assert.Single(await Servico(db).RotasDoDiaAsync(outra.Tablet));
        Assert.Equal(["retorno:", "casa:"], daOutra.Paradas.Select(p => p.Id[..(p.Id.IndexOf(':') + 1)]));
        Assert.All(daOutra.Paradas, p => Assert.Equal(RotaTabletService.Volta, p.Trecho));
        Assert.DoesNotContain("espera", daOutra.Paradas[0].Observacoes); // veio buscar; não ficou esperando
        await Assert.ThrowsAsync<ConflitoException>(() => rastreamento.PuxarAsync(new PuxarPacienteRequest(rotaVolta, ana)));
    }

    [Fact]
    public async Task Quem_nao_estava_na_unidade_quando_a_van_saiu_fica_aguardando_retorno()
    {
        await using var db = fixture.CriarDbContext();
        var (f, rotaId, ana, _) = await ViagemDeHojeAsync(db);
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());
        var viagem = Viagem(db);

        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, ana));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, ana));
        await MarcarNaVoltaAsync(viagem, f, rotaId, TipoEventoViagem.Ausencia, ana);

        Assert.Equal(StatusSessao.AguardandoRetorno, (await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == ana)).Status);
        var naFila = (await Rastreamento(db).ListarAguardandoAsync(null)).Single(x => x.SessaoId == ana);
        Assert.True(naFila.ForaDoMunicipio);
        // O translado da ida segue contado.
        Assert.Equal(StatusFaturamento.Pendente,
            (await db.RegistrosFaturamento.AsNoTracking().SingleAsync(r => r.SessaoId == ana)).Status);
    }
}
