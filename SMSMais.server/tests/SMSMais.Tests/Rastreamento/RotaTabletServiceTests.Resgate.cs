using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Rastreamento;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Core.Translado;
using SMSMais.Core.Translado.Dtos;
using SMSMais.Core.Translado.Resgate;
using SMSMais.Core.Translado.Viagem;
using SMSMais.Core.Tratamentos;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Resgate (decisões de 19/10): quem desembarca na unidade entra na lista "Pacientes disponíveis para
/// resgate" com a previsão; o paciente, a central ou um motorista avisam que ele já está no local; qualquer
/// carro marca quem vai buscar, mesmo que não o tenha levado, e só se couber nele naquela hora. A central
/// nunca põe no carro mais gente do que ele leva. Pacientes fictícios.
/// </summary>
public partial class RotaTabletServiceTests
{
    private readonly IAvisoAoPacienteTransporte _entrega = Substitute.For<IAvisoAoPacienteTransporte>();

    private ResgateService Resgates(SmsMaisDbContext db) =>
        new(db, _resolver, Substitute.For<IRastreamentoNotificador>(), _entrega, NullLogger<ResgateService>.Instance);

    private async Task NoResgateAsync(
        ViagemService viagem, Frota f, Guid rotaId, TipoEventoViagem tipo, Guid sessao,
        PessoaViagem? pessoa = null, int? ordem = null) =>
        await viagem.RegistrarDoTabletAsync(f.Tablet,
            Marca(rotaId, tipo, sessao, pessoa, ordem, motoristaId: f.MotoristaId) with { Resgate = true });

    private void SemNomes() =>
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());

    /// <summary>A van A leva Ana (com acompanhante) e Beatriz à clínica e as duas descem lá.</summary>
    private async Task<(Frota A, Guid RotaA, Guid Ana, Guid Beatriz)> NaClinicaAsync(SmsMaisDbContext db)
    {
        var (f, rotaId, ana, beatriz) = await ViagemDeHojeAsync(db);
        SemNomes();
        var viagem = Viagem(db);
        foreach (var s in new[] { ana, beatriz })
        {
            await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, s));
            await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, s));
        }
        return (f, rotaId, ana, beatriz);
    }

    /// <summary>Outro carro, já em andamento, levando <paramref name="aBordo"/> pacientes (sem acompanhante)
    /// para a mesma clínica, ainda sem descer.</summary>
    private async Task<(Frota B, Guid RotaB, List<Guid> Levando)> OutroCarroAsync(SmsMaisDbContext db, Guid unidadeDe, int aBordo)
    {
        var b = await VeiculoComTabletAsync(db);
        var rota = await RotaAsync(db, b, StatusRota.EmAndamento);
        var (unidadeId, tipoId) = await (from s in db.Sessoes where s.Id == unidadeDe
                                         join t in db.Tratamentos on s.TratamentoId equals t.Id
                                         select new ValueTuple<Guid, Guid>(t.UnidadeAtendimentoId, t.TipoTratamentoId!.Value)).SingleAsync();
        var levando = new List<Guid>();
        for (var i = 0; i < aBordo; i++)
        {
            var s = await SessaoAsync(db, Guid.NewGuid(), unidadeId, tipoId, status: StatusSessao.Confirmada);
            await AlocarAsync(db, rota, s, b.Assentos[i], TipoAlocacao.Paciente, i + 1);
            levando.Add(s);
        }
        return (b, rota, levando);
    }

    [Fact]
    public async Task Quem_desce_na_unidade_entra_na_lista_com_a_previsao_e_sem_volta_automatica()
    {
        await using var db = fixture.CriarDbContext();
        var (a, rotaA, ana, _) = await NaClinicaAsync(db); // clínica fora do município

        // Ninguém entra sozinho no resgate, nem fora do município: a van não "espera" automaticamente.
        var rota = Assert.Single(await Servico(db).RotasDoDiaAsync(a.Tablet));
        Assert.DoesNotContain(rota.Paradas, p => p.Trecho == RotaTabletService.Resgate);

        var lista = await Resgates(db).ListarAsync(rotaA);
        var item = lista.Disponiveis.Single(x => x.SessaoId == ana);
        var chegada = (await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == ana)).HoraChegadaUnidade!.Value;
        Assert.Equal(chegada.AddMinutes(240), item.DisponivelAs); // chegada + tempo médio da Hemodiálise
        Assert.Contains(item.Disponibilidade, new[] { DisponibilidadeResgate.EmAtendimento, DisponibilidadeResgate.PelaPrevisao });
        Assert.Equal(1, item.Acompanhantes);
        Assert.True(item.ForaDoMunicipio);
        Assert.Equal(true, item.Cabe); // a van que levou está vazia de novo
        Assert.Equal(4, lista.LugaresLivres);
    }

    [Fact]
    public async Task Aviso_de_que_esta_no_local_poe_o_paciente_no_topo()
    {
        await using var db = fixture.CriarDbContext();
        var (_, _, ana, beatriz) = await NaClinicaAsync(db);
        var resgates = Resgates(db);
        var pacienteDaAna = await (from s in db.Sessoes where s.Id == ana
                                   join t in db.Tratamentos on s.TratamentoId equals t.Id
                                   select t.PacienteId).SingleAsync();

        await resgates.ResponderPeloPacienteAsync(pacienteDaAna, ana, disponivel: true);
        await resgates.AvisarNoLocalAsync(beatriz, CanalAvisoResgate.Central);
        // Sessão de outra pessoa: o paciente não avisa por ela.
        await Assert.ThrowsAsync<NaoEncontradoException>(() => resgates.ResponderPeloPacienteAsync(pacienteDaAna, beatriz, disponivel: true));

        var minhas = (await resgates.ListarAsync(null)).Disponiveis.Where(x => x.SessaoId == ana || x.SessaoId == beatriz).ToList();
        Assert.All(minhas, x => Assert.Equal(DisponibilidadeResgate.NoLocal, x.Disponibilidade));
        Assert.Equal(CanalAvisoResgate.Paciente, minhas.Single(x => x.SessaoId == ana).AvisadoPor);
        Assert.Equal(CanalAvisoResgate.Central, minhas.Single(x => x.SessaoId == beatriz).AvisadoPor);
        Assert.All(minhas, x => Assert.Equal(0, x.AguardandoMinutos)); // acabaram de avisar
        // No app do paciente: está esperando, ainda sem carro.
        var noApp = await resgates.DoPacienteAsync(pacienteDaAna);
        Assert.Equal(DisponibilidadeResgate.NoLocal, noApp!.Disponibilidade);
        Assert.Null(noApp.VeiculoPlaca);
    }

    [Fact]
    public async Task Outro_carro_resgata_quem_nao_levou_e_so_cabe_quem_tem_lugar_na_hora()
    {
        await using var db = fixture.CriarDbContext();
        var (a, _, ana, beatriz) = await NaClinicaAsync(db);
        var (b, rotaB, levando) = await OutroCarroAsync(db, ana, aBordo: 3); // 1 lugar livre agora
        var resgates = Resgates(db);

        // Ana vai com acompanhante: são 2 pessoas para 1 lugar.
        var cheio = await Assert.ThrowsAsync<ConflitoException>(() => resgates.MarcarDoTabletAsync(b.Tablet, new(rotaB, [ana])));
        Assert.Equal("resgate.sem_lugar", cheio.Codigo);
        Assert.Contains("1 lugar livre", cheio.Message);
        var lista = await resgates.ListarDoTabletAsync(b.Tablet, rotaB);
        Assert.Equal(1, lista.LugaresLivres);
        Assert.Equal(new CarroAgoraDto(0, 1, 0, false, 3, false, false), lista.Carro); // sobrou o de acompanhante
        Assert.False(lista.Disponiveis.Single(x => x.SessaoId == ana).Cabe);
        Assert.True(lista.Disponiveis.Single(x => x.SessaoId == beatriz).Cabe);

        await resgates.MarcarDoTabletAsync(b.Tablet, new(rotaB, [beatriz]));

        // Quem estava no carro B desce na clínica: os lugares voltam e a Ana cabe.
        var viagem = Viagem(db);
        foreach (var s in levando)
        {
            await viagem.RegistrarDoTabletAsync(b.Tablet, Marca(rotaB, TipoEventoViagem.Embarque, s));
            await viagem.RegistrarDoTabletAsync(b.Tablet, Marca(rotaB, TipoEventoViagem.Desembarque, s));
        }
        await resgates.MarcarDoTabletAsync(b.Tablet, new(rotaB, [ana]));

        // A van que levou a Ana não marca mais: ficou com o carro B.
        var rotaA2 = await RotaAsync(db, a, StatusRota.EmAndamento);
        var outro = await Assert.ThrowsAsync<ConflitoException>(() => resgates.MarcarDoTabletAsync(a.Tablet, new(rotaA2, [ana])));
        Assert.Equal("resgate.ja_marcado", outro.Codigo);

        // As duas são buscadas na mesma parada (marcadas na mesma unidade antes do embarque) e depois entregues.
        var rota = (await Servico(db).RotasDoDiaAsync(b.Tablet)).Single(r => r.RotaId == rotaB);
        var resgate = rota.Paradas.Where(p => p.Trecho == RotaTabletService.Resgate).ToList();
        var busca = Assert.Single(resgate, p => p.Tipo == TipoParada.Coleta);
        Assert.StartsWith("Resgate — ", busca.Rotulo);
        Assert.Equal([beatriz, ana], busca.Passageiros.Select(p => p.SessaoId));
        Assert.All(resgate.Where(p => p.Tipo == TipoParada.Destino), p => Assert.Equal(busca.Lote, p.Lote));
        Assert.Equal(2, resgate.Count(p => p.Tipo == TipoParada.Destino));
        Assert.NotNull(busca.Passageiros.Single(p => p.SessaoId == ana).Acompanhantes.Single().Assento);
        var marcadas = (await resgates.ListarDoTabletAsync(b.Tablet, rotaB)).Marcados.Select(m => m.SessaoId);
        Assert.Equal(new[] { ana, beatriz }.Order(), marcadas.Order());
    }

    [Fact]
    public async Task Dois_carros_marcando_o_mesmo_paciente_ao_mesmo_tempo_so_um_fica_com_ele()
    {
        await using var db = fixture.CriarDbContext();
        var (_, _, _, beatriz) = await NaClinicaAsync(db);
        var (b, rotaB, _) = await OutroCarroAsync(db, beatriz, aBordo: 0);
        var (c, rotaC, _) = await OutroCarroAsync(db, beatriz, aBordo: 0);

        await using var db1 = fixture.CriarDbContext();
        await using var db2 = fixture.CriarDbContext();
        var t1 = Resgates(db1).MarcarDoTabletAsync(b.Tablet, new(rotaB, [beatriz]));
        var t2 = Resgates(db2).MarcarDoTabletAsync(c.Tablet, new(rotaC, [beatriz]));
        var resultados = await Task.WhenAll(Capturar(t1), Capturar(t2));

        Assert.Single(resultados, r => r is null);
        Assert.Single(resultados, r => r is ConflitoException { Codigo: "resgate.ja_marcado" });
        Assert.Equal(1, await db.Alocacoes.CountAsync(x => x.SessaoId == beatriz && x.Parada == TipoParada.Retorno));

        static async Task<Exception?> Capturar(Task t)
        {
            try { await t; return null; }
            catch (Exception ex) { return ex; }
        }
    }

    [Fact]
    public async Task Quem_nao_estava_no_local_volta_para_a_lista_e_pode_ser_marcado_de_novo()
    {
        await using var db = fixture.CriarDbContext();
        var (_, _, ana, _) = await NaClinicaAsync(db);
        var (b, rotaB, _) = await OutroCarroAsync(db, ana, aBordo: 0);
        var resgates = Resgates(db);
        var viagem = Viagem(db);
        await resgates.AvisarNoLocalAsync(ana, CanalAvisoResgate.Paciente);
        await resgates.MarcarDoTabletAsync(b.Tablet, new(rotaB, [ana]));

        await NoResgateAsync(viagem, b, rotaB, TipoEventoViagem.Ausencia, ana);
        await NoResgateAsync(viagem, b, rotaB, TipoEventoViagem.Ausencia, ana, PessoaViagem.Acompanhante, 1);

        var deNovo = (await resgates.ListarAsync(null)).Disponiveis.Single(x => x.SessaoId == ana);
        Assert.NotEqual(DisponibilidadeResgate.NoLocal, deNovo.Disponibilidade); // o aviso caiu
        Assert.NotNull(deNovo.NaoEstavaNoLocalEm);
        Assert.DoesNotContain((await Servico(db).RotasDoDiaAsync(b.Tablet)).Single().Paradas, p => p.Trecho == RotaTabletService.Resgate);
        // A marca do acompanhante fica registrada como veio (não vira "não aplicado").
        Assert.All(await db.EventosViagem.AsNoTracking().Where(e => e.RotaId == rotaB).ToListAsync(), e => Assert.True(e.Aplicado));

        // Marcado de novo, começa sem a marca de ausente.
        await resgates.MarcarDoTabletAsync(b.Tablet, new(rotaB, [ana]));
        var busca = (await Servico(db).RotasDoDiaAsync(b.Tablet)).Single().Paradas
            .Single(p => p.Trecho == RotaTabletService.Resgate && p.Tipo == TipoParada.Coleta);
        Assert.False(busca.Concluida);
        Assert.Null(busca.Passageiros.Single().Situacao);
    }

    [Fact]
    public async Task Resgate_feito_preenche_a_volta_e_a_rota_encerrada_devolve_quem_nao_foi_buscado()
    {
        await using var db = fixture.CriarDbContext();
        var (_, _, ana, beatriz) = await NaClinicaAsync(db);
        var (b, rotaB, _) = await OutroCarroAsync(db, ana, aBordo: 0);
        var resgates = Resgates(db);
        var viagem = Viagem(db);
        await resgates.MarcarDoTabletAsync(b.Tablet, new(rotaB, [beatriz, ana]));
        // Já embarcou: não dá mais para desmarcar.
        await NoResgateAsync(viagem, b, rotaB, TipoEventoViagem.Embarque, beatriz);
        await Assert.ThrowsAsync<ConflitoException>(() => resgates.DesmarcarAsync(rotaB, beatriz));
        await NoResgateAsync(viagem, b, rotaB, TipoEventoViagem.Desembarque, beatriz);

        var sBeatriz = await db.Sessoes.AsNoTracking().SingleAsync(s => s.Id == beatriz);
        Assert.Equal(StatusSessao.Realizada, sBeatriz.Status);
        Assert.NotNull(sBeatriz.HoraSaidaUnidade);
        Assert.NotNull(sBeatriz.HoraChegadaResidencia);
        Assert.Equal(b.MotoristaId, sBeatriz.MotoristaVoltaId);
        Assert.Equal(b.Tablet.VeiculoId, sBeatriz.VeiculoVoltaId);

        // A rota acaba sem ter buscado a Ana: ela volta para a lista.
        await viagem.RegistrarDoTabletAsync(b.Tablet, Marca(rotaB, TipoEventoViagem.Conclusao, motoristaId: b.MotoristaId));
        var lista = (await resgates.ListarAsync(null)).Disponiveis;
        Assert.Contains(lista, x => x.SessaoId == ana);
        Assert.DoesNotContain(lista, x => x.SessaoId == beatriz);
        // O translado da ida segue contado.
        Assert.Equal(StatusFaturamento.Pendente,
            (await db.RegistrosFaturamento.AsNoTracking().SingleAsync(r => r.SessaoId == ana)).Status);
    }

    [Fact]
    public async Task No_app_o_paciente_ve_o_carro_que_vai_busca_lo()
    {
        await using var db = fixture.CriarDbContext();
        var (_, _, ana, _) = await NaClinicaAsync(db);
        var (b, rotaB, _) = await OutroCarroAsync(db, ana, aBordo: 0);
        var resgates = Resgates(db);
        var pacienteDaAna = await (from s in db.Sessoes where s.Id == ana
                                   join t in db.Tratamentos on s.TratamentoId equals t.Id
                                   select t.PacienteId).SingleAsync();

        await resgates.MarcarAsync(rotaB, [ana]); // a central marca para o carro B

        var noApp = await resgates.DoPacienteAsync(pacienteDaAna);
        var placa = await db.Veiculos.Where(v => v.Id == b.Tablet.VeiculoId).Select(v => v.Placa).SingleAsync();
        Assert.Equal(placa, noApp!.VeiculoPlaca);
        Assert.False(noApp.Embarcou);
    }

    [Fact]
    public async Task A_central_nao_poe_no_carro_mais_gente_do_que_ele_leva()
    {
        await using var db = fixture.CriarDbContext();
        var f = await VeiculoComTabletAsync(db,
            TipoAssento.Cadeirante, TipoAssento.Passageiro, TipoAssento.Passageiro, TipoAssento.Acompanhante);
        var (unidadeId, tipoId) = await DestinoAsync(db);
        var rotaId = await RotaAsync(db, f);
        var translados = new TransladoService(db, _resolver);
        SemNomes();

        // Cadeirante na própria cadeira só no assento de cadeirante.
        var cadeirante = await SessaoAsync(db, Guid.NewGuid(), unidadeId, tipoId, MobilidadeTransporte.CadeiranteVeiculoAdaptado);
        var cadeira = await Assert.ThrowsAsync<ConflitoException>(() =>
            translados.CriarAlocacaoAsync(rotaId, new CriarAlocacaoRequest(cadeirante, f.Assentos[1].Id)));
        Assert.Equal("assento.cadeirante", cadeira.Codigo);
        await translados.CriarAlocacaoAsync(rotaId, new CriarAlocacaoRequest(cadeirante, f.Assentos[0].Id));

        // Com 2 acompanhantes: o paciente e cada acompanhante levam um lugar (3 livres, cabe).
        var comDois = await SessaoComAcompanhantesAsync(db, unidadeId, tipoId, 2);
        await translados.CriarAlocacaoAsync(rotaId, new CriarAlocacaoRequest(comDois, f.Assentos[1].Id));
        var deles = await db.Alocacoes.AsNoTracking().Where(a => a.RotaDiariaId == rotaId && a.SessaoId == comDois).ToListAsync();
        Assert.Equal(3, deles.Count);
        Assert.Equal(2, deles.Count(a => a.Tipo == TipoAlocacao.Acompanhante));

        // O carro está cheio: mais ninguém.
        var outro = await SessaoAsync(db, Guid.NewGuid(), unidadeId, tipoId);
        await Assert.ThrowsAsync<ConflitoException>(() =>
            translados.CriarAlocacaoAsync(rotaId, new CriarAlocacaoRequest(outro, f.Assentos[2].Id)));

        // Tirar o paciente tira os acompanhantes junto.
        await translados.RemoverAlocacaoAsync(rotaId, deles.First(a => a.Tipo == TipoAlocacao.Paciente).Id);
        Assert.False(await db.Alocacoes.AnyAsync(a => a.RotaDiariaId == rotaId && a.SessaoId == comDois));

        // Com mais um paciente no carro, sobram 2 lugares: o de 2 acompanhantes (3 pessoas) não cabe.
        await translados.CriarAlocacaoAsync(rotaId, new CriarAlocacaoRequest(outro, f.Assentos[2].Id));
        var seco = await Assert.ThrowsAsync<ConflitoException>(() =>
            translados.CriarAlocacaoAsync(rotaId, new CriarAlocacaoRequest(comDois, f.Assentos[1].Id)));
        Assert.Equal("rota.sem_lugar_acompanhante", seco.Codigo);
    }

    [Fact]
    public async Task Mais_um_acompanhante_numa_viagem_ja_na_rota_so_se_couber()
    {
        await using var db = fixture.CriarDbContext();
        var f = await VeiculoComTabletAsync(db, TipoAssento.Passageiro, TipoAssento.Acompanhante);
        var (unidadeId, tipoId) = await DestinoAsync(db);
        var rotaId = await RotaAsync(db, f);
        var translados = new TransladoService(db, _resolver);
        var sessao = await SessaoComAcompanhantesAsync(db, unidadeId, tipoId, 0, limite: 2);
        await translados.CriarAlocacaoAsync(rotaId, new CriarAlocacaoRequest(sessao, f.Assentos[0].Id));
        var (tratamentoId, pacienteId) = await (from s in db.Sessoes where s.Id == sessao
                                               join t in db.Tratamentos on s.TratamentoId equals t.Id
                                               select new ValueTuple<Guid, Guid>(t.Id, t.PacienteId)).SingleAsync();
        var um = await AcompanhanteAsync(db, pacienteId, "Bia Simulada");
        var dois = await AcompanhanteAsync(db, pacienteId, "Carlos Simulado");
        var usuario = Substitute.For<IUsuarioAtualAccessor>();
        usuario.UsuarioId.Returns(Guid.NewGuid());
        var painel = new TratamentosService(db, _resolver, Faturamento(db), _pacientes, usuario,
            NullLogger<TratamentosService>.Instance);

        // Um acompanhante: ganha o lugar que sobrava.
        await painel.DefinirAcompanhantesDaSessaoAsync(tratamentoId, sessao, new DefinirAcompanhantesSessaoRequest([um]));
        Assert.Equal(1, await db.Alocacoes.CountAsync(a => a.SessaoId == sessao && a.Tipo == TipoAlocacao.Acompanhante));

        // Dois: não há lugar para o segundo — nada muda.
        db.ChangeTracker.Clear();
        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            painel.DefinirAcompanhantesDaSessaoAsync(tratamentoId, sessao, new DefinirAcompanhantesSessaoRequest([um, dois])));
        Assert.Equal("sessao.sem_lugar_acompanhante", ex.Codigo);
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.SessoesAcompanhantes.CountAsync(x => x.SessaoId == sessao));

        // Nenhum: o lugar fica livre.
        await painel.DefinirAcompanhantesDaSessaoAsync(tratamentoId, sessao, new DefinirAcompanhantesSessaoRequest([]));
        Assert.Equal(0, await db.Alocacoes.CountAsync(a => a.SessaoId == sessao && a.Tipo == TipoAlocacao.Acompanhante));
    }

    [Fact]
    public async Task Estourou_a_previsao_pergunta_e_cada_nao_pergunta_de_novo_em_10_minutos()
    {
        await using var db = fixture.CriarDbContext();
        var (_, _, ana, _) = await NaClinicaAsync(db);
        var resgates = Resgates(db);
        var pacienteDaAna = await (from s in db.Sessoes where s.Id == ana
                                   join t in db.Tratamentos on s.TratamentoId equals t.Id
                                   select t.PacienteId).SingleAsync();
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>
            {
                [pacienteDaAna] = new(pacienteDaAna, "ANA SIMULADA TESTE", null, null, null, Sexo.Feminino),
            });
        _entrega.PerguntarSeDisponivelAsync(default, default!, default).ReturnsForAnyArgs(true);

        // Ainda dentro da previsão: nada a perguntar.
        Assert.False((await resgates.DoPacienteAsync(pacienteDaAna))!.PerguntaPendente);
        await resgates.PerguntarQuemPassouDaPrevisaoAsync();
        await _entrega.DidNotReceive().PerguntarSeDisponivelAsync(pacienteDaAna, Arg.Any<string>(), ana, Arg.Any<CancellationToken>());

        // A central marcou o retorno para 5 minutos atrás: a previsão estourou.
        var agora = FusoBrasilia.ParaExibicao(DateTime.UtcNow);
        var sessao = await db.Sessoes.SingleAsync(s => s.Id == ana);
        sessao.HoraPrevistaRetorno = TimeOnly.FromDateTime(agora) > new TimeOnly(0, 5)
            ? TimeOnly.FromDateTime(agora).AddMinutes(-5) : TimeOnly.MinValue;
        await db.SaveChangesAsync();

        Assert.True((await resgates.DoPacienteAsync(pacienteDaAna))!.PerguntaPendente);
        await resgates.PerguntarQuemPassouDaPrevisaoAsync();
        await resgates.PerguntarQuemPassouDaPrevisaoAsync(); // uma vez por rodada
        await _entrega.Received(1).PerguntarSeDisponivelAsync(pacienteDaAna, "Ana", ana, Arg.Any<CancellationToken>());

        // "Não, ainda não terminei": volta para "em atendimento", e a pergunta volta em 10 minutos.
        await resgates.ResponderPeloPacienteAsync(pacienteDaAna, ana, disponivel: false);
        var noApp = (await resgates.DoPacienteAsync(pacienteDaAna))!;
        Assert.False(noApp.PerguntaPendente);
        Assert.NotNull(noApp.ProximaPerguntaAs);
        var naLista = (await resgates.ListarAsync(null)).Disponiveis.Single(x => x.SessaoId == ana);
        Assert.Equal(DisponibilidadeResgate.EmAtendimento, naLista.Disponibilidade);
        Assert.InRange(naLista.DisponivelEmMinutos!.Value, 9, 10);
        Assert.NotNull(naLista.AindaEmAtendimentoEm);

        // 10 minutos depois (perguntado há 12 min, "não" há 11): pergunta de novo.
        db.ChangeTracker.Clear();
        await db.Sessoes.Where(s => s.Id == ana).ExecuteUpdateAsync(u => u
            .SetProperty(s => s.PerguntaResgateEnviadaEm, DateTime.UtcNow.AddMinutes(-12))
            .SetProperty(s => s.AindaEmAtendimentoEm, DateTime.UtcNow.AddMinutes(-11)));
        Assert.True((await resgates.DoPacienteAsync(pacienteDaAna))!.PerguntaPendente);
        await resgates.PerguntarQuemPassouDaPrevisaoAsync();
        await _entrega.Received(2).PerguntarSeDisponivelAsync(pacienteDaAna, "Ana", ana, Arg.Any<CancellationToken>());

        // "Sim, já estou disponível": vai para o topo da lista dos motoristas e a pergunta para.
        await resgates.ResponderPeloPacienteAsync(pacienteDaAna, ana, disponivel: true);
        var pronta = (await resgates.ListarAsync(null)).Disponiveis.Single(x => x.SessaoId == ana);
        Assert.Equal(DisponibilidadeResgate.NoLocal, pronta.Disponibilidade);
        Assert.Equal(CanalAvisoResgate.Paciente, pronta.AvisadoPor);
        Assert.False((await resgates.DoPacienteAsync(pacienteDaAna))!.PerguntaPendente);
        await resgates.PerguntarQuemPassouDaPrevisaoAsync();
        await _entrega.Received(2).PerguntarSeDisponivelAsync(pacienteDaAna, Arg.Any<string>(), ana, Arg.Any<CancellationToken>());
    }

    private static async Task<Guid> SessaoComAcompanhantesAsync(
        SmsMaisDbContext db, Guid unidadeId, Guid tipoId, int quantos, int limite = 2)
    {
        var pacienteId = Guid.NewGuid();
        var sessao = await SessaoAsync(db, pacienteId, unidadeId, tipoId);
        var s = await db.Sessoes.Include(x => x.Tratamento).SingleAsync(x => x.Id == sessao);
        s.Tratamento!.QuantidadeAcompanhantes = limite;
        for (var i = 0; i < quantos; i++)
        {
            var a = await AcompanhanteAsync(db, pacienteId, $"Acompanhante Simulado {i + 1}");
            db.SessoesAcompanhantes.Add(new SessaoAcompanhante { SessaoId = sessao, AcompanhanteId = a });
        }
        s.AcompanhanteEsperado = quantos > 0 ? true : null;
        await db.SaveChangesAsync();
        return sessao;
    }

    private static async Task<Guid> AcompanhanteAsync(SmsMaisDbContext db, Guid pacienteId, string nome)
    {
        var a = new Acompanhante
        {
            Id = Guid.NewGuid(), PacienteId = pacienteId, Cpf = Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString(), Nome = nome,
            DataNascimento = new DateOnly(1980, 1, 1), Parentesco = ParentescoAcompanhante.Filho, CriadoEm = DateTime.UtcNow,
        };
        db.Acompanhantes.Add(a);
        await db.SaveChangesAsync();
        return a.Id;
    }
}
