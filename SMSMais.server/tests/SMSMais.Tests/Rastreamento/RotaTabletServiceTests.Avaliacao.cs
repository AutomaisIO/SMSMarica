using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Avaliacoes;
using SMSMais.Core.Avaliacoes.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Etapa 5: o paciente avalia pelo app do cidadão a viagem que aconteceu — uma vez, até 30 dias depois —
/// e a nota fica com quem levou, o veículo e a rota daquela viagem. Pacientes fictícios.
/// </summary>
public partial class RotaTabletServiceTests
{
    private AvaliacoesService Avaliacoes(SmsMaisDbContext db) => new(db, _resolver);

    private static async Task<Guid> PacienteDaSessaoAsync(SmsMaisDbContext db, Guid sessaoId) =>
        await (from s in db.Sessoes where s.Id == sessaoId
               join t in db.Tratamentos on s.TratamentoId equals t.Id
               select t.PacienteId).SingleAsync();

    [Fact]
    public async Task Paciente_avalia_a_viagem_feita_e_a_nota_fica_com_motorista_veiculo_e_rota()
    {
        await using var db = fixture.CriarDbContext();
        var (f, rotaId, ana, beatriz) = await ViagemDeHojeAsync(db);
        var viagem = Viagem(db);
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Embarque, ana, motoristaId: f.MotoristaId));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, ana, motoristaId: f.MotoristaId));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Ausencia, beatriz));
        var pacienteAna = await PacienteDaSessaoAsync(db, ana);
        var pacienteBeatriz = await PacienteDaSessaoAsync(db, beatriz);
        _resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo>());
        var avaliacoes = Avaliacoes(db);

        var paraAvaliar = Assert.Single(await avaliacoes.ViagensParaAvaliarAsync(pacienteAna));
        Assert.Equal(ana, paraAvaliar.SessaoId);
        Assert.Null(paraAvaliar.Nota);
        Assert.Equal("Motorista", paraAvaliar.Motorista); // só o primeiro nome
        Assert.Empty(await avaliacoes.ViagensParaAvaliarAsync(pacienteBeatriz)); // faltou: não houve viagem

        await avaliacoes.AvaliarPeloPacienteAsync(pacienteAna, ana, new AvaliarViagemRequest(5, "  Motorista muito atencioso  "));

        var a = await db.Avaliacoes.AsNoTracking().SingleAsync(x => x.SessaoId == ana);
        Assert.Equal((5, "Motorista muito atencioso", CanalAvaliacao.AppCidadao), (a.Nota, a.Comentario, a.Canal));
        Assert.Equal((f.MotoristaId, f.Tablet.VeiculoId, rotaId), (a.MotoristaId!.Value, a.VeiculoId!.Value, a.RotaId!.Value));
        Assert.Equal(5, (await avaliacoes.ViagensParaAvaliarAsync(pacienteAna)).Single().Nota);

        var noPainel = (await avaliacoes.ListarAsync()).Single(x => x.SessaoId == ana);
        Assert.Equal(("Motorista Simulado", CanalAvaliacao.AppCidadao), (noPainel.MotoristaNome, noPainel.Canal));
        Assert.NotNull(noPainel.VeiculoPlaca);
        Assert.NotNull(noPainel.DataViagem);
        // A nota do paciente é dele: a equipe não reescreve.
        await Assert.ThrowsAsync<ConflitoException>(() =>
            avaliacoes.AtualizarAsync(noPainel.Id, new AtualizarAvaliacaoRequest(1, "editada")));
    }

    [Fact]
    public async Task So_o_proprio_paciente_so_viagem_feita_uma_vez_e_nota_de_1_a_5()
    {
        await using var db = fixture.CriarDbContext();
        var (f, rotaId, ana, beatriz) = await ViagemDeHojeAsync(db);
        var viagem = Viagem(db);
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Desembarque, ana));
        await viagem.RegistrarDoTabletAsync(f.Tablet, Marca(rotaId, TipoEventoViagem.Ausencia, beatriz));
        var pacienteAna = await PacienteDaSessaoAsync(db, ana);
        var pacienteBeatriz = await PacienteDaSessaoAsync(db, beatriz);
        var avaliacoes = Avaliacoes(db);

        // A sessão de outra pessoa nem "existe" para quem pergunta.
        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            avaliacoes.AvaliarPeloPacienteAsync(pacienteBeatriz, ana, new AvaliarViagemRequest(1, null)));
        await Assert.ThrowsAsync<ConflitoException>(() =>
            avaliacoes.AvaliarPeloPacienteAsync(pacienteBeatriz, beatriz, new AvaliarViagemRequest(1, null)));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            avaliacoes.AvaliarPeloPacienteAsync(pacienteAna, ana, new AvaliarViagemRequest(0, null)));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            avaliacoes.AvaliarPeloPacienteAsync(pacienteAna, ana, new AvaliarViagemRequest(4, new string('x', 501))));

        await avaliacoes.AvaliarPeloPacienteAsync(pacienteAna, ana, new AvaliarViagemRequest(4, null));
        await Assert.ThrowsAsync<ConflitoException>(() =>
            avaliacoes.AvaliarPeloPacienteAsync(pacienteAna, ana, new AvaliarViagemRequest(1, null)));
        Assert.Equal(4, (await db.Avaliacoes.AsNoTracking().SingleAsync(x => x.SessaoId == ana)).Nota);
        // O resgate só se avalia depois de chegar em casa.
        var semResgate = await Assert.ThrowsAsync<ConflitoException>(() =>
            avaliacoes.AvaliarPeloPacienteAsync(pacienteAna, ana, new AvaliarViagemRequest(5, null, TrechoViagem.Resgate)));
        Assert.Equal("avaliacao.resgate_nao_feito", semResgate.Codigo);
    }

    [Fact]
    public async Task Ida_e_resgate_pedem_avaliacao_e_cada_nota_fica_com_o_motorista_do_trecho()
    {
        await using var db = fixture.CriarDbContext();
        var (a, rotaA, ana, _) = await NaClinicaAsync(db); // a van A levou e a Ana desceu na clínica
        var paciente = await PacienteDaSessaoAsync(db, ana);
        await _entrega.Received(1).PedirAvaliacaoAsync(
            paciente, Arg.Any<string>(), ana, TrechoViagem.Ida, "Motorista", Arg.Any<CancellationToken>());

        // O carro B resgata e deixa em casa: pede a avaliação do motorista do resgate.
        var (b, rotaB, _) = await OutroCarroAsync(db, ana, aBordo: 0);
        await Resgates(db).MarcarDoTabletAsync(b.Tablet, new(rotaB, [ana]));
        var viagem = Viagem(db);
        await NoResgateAsync(viagem, b, rotaB, TipoEventoViagem.Embarque, ana);
        await NoResgateAsync(viagem, b, rotaB, TipoEventoViagem.Desembarque, ana);
        await _entrega.Received(1).PedirAvaliacaoAsync(
            paciente, Arg.Any<string>(), ana, TrechoViagem.Resgate, Arg.Any<string?>(), Arg.Any<CancellationToken>());

        var avaliacoes = Avaliacoes(db);
        var lista = await avaliacoes.ViagensParaAvaliarAsync(paciente);
        Assert.Equal([TrechoViagem.Resgate, TrechoViagem.Ida], lista.Select(x => x.Trecho));
        Assert.All(lista, x => Assert.NotNull(x.Hora));

        await avaliacoes.AvaliarPeloPacienteAsync(paciente, ana, new AvaliarViagemRequest(5, null, TrechoViagem.Ida));
        await avaliacoes.AvaliarPeloPacienteAsync(paciente, ana, new AvaliarViagemRequest(3, "Demorou", TrechoViagem.Resgate));
        await Assert.ThrowsAsync<ConflitoException>(() =>
            avaliacoes.AvaliarPeloPacienteAsync(paciente, ana, new AvaliarViagemRequest(1, null, TrechoViagem.Resgate)));

        var notas = await db.Avaliacoes.AsNoTracking().Where(x => x.SessaoId == ana).ToListAsync();
        var ida = notas.Single(x => x.Trecho == TrechoViagem.Ida);
        var resgate = notas.Single(x => x.Trecho == TrechoViagem.Resgate);
        Assert.Equal((a.MotoristaId, a.Tablet.VeiculoId, rotaA), (ida.MotoristaId!.Value, ida.VeiculoId!.Value, ida.RotaId!.Value));
        Assert.Equal((b.MotoristaId, b.Tablet.VeiculoId, rotaB), (resgate.MotoristaId!.Value, resgate.VeiculoId!.Value, resgate.RotaId!.Value));
        Assert.Equal(TrechoViagem.Resgate, (await avaliacoes.ListarAsync()).Single(x => x.Id == resgate.Id).Trecho);
    }
}
