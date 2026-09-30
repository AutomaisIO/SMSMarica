using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Tratamentos;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;
using static SMSMais.Tests.Tratamentos.TransporteFabrica;

namespace SMSMais.Tests.Tratamentos;

/// <summary>
/// Atendimento do transporte: agenda por dias da semana (N sessões ou contínuo), tempo médio do
/// tipo, limite de acompanhantes (o 2º só com liberação), troca de agenda que preserva o que já
/// aconteceu, e encerrar que não deixa viagem futura pendurada.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class TratamentosServiceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Cadastrar_gera_as_N_sessoes_nos_dias_marcados_e_o_tempo_vem_do_tipo()
    {
        await using var db = fixture.CriarDbContext();
        var tipo = await NovoTipoAsync(db, tempoMedio: 270);
        var destino = await NovoDestinoAsync(db);
        var inicio = Proxima(DayOfWeek.Monday);

        var id = await Servico(db).CadastrarAsync(Novo(destino, tipo,
            new AgendaRequest(inicio, Segunda | Quarta | Sexta, 12, false)));

        var dto = await Servico(db).ObterPorIdAsync(id);
        Assert.Equal(270, dto.TempoMedioMinutos);
        Assert.Equal(12, dto.Sessoes.Count);
        Assert.All(dto.Sessoes, s => Assert.Contains(s.DataPrevista.DayOfWeek,
            new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }));
        Assert.Equal(inicio, dto.Sessoes[0].DataPrevista);
        Assert.Equal(dto.Sessoes[^1].DataPrevista, dto.Agenda.SessoesGeradasAte);
        Assert.All(dto.Sessoes, s => Assert.Null(s.HoraPrevistaBusca));
    }

    [Fact]
    public async Task Continuo_gera_ate_o_fim_do_mes_seguinte_sem_total()
    {
        await using var db = fixture.CriarDbContext();
        var inicio = Proxima(DayOfWeek.Tuesday);

        var id = await Servico(db).CadastrarAsync(Novo(await NovoDestinoAsync(db), await NovoTipoAsync(db),
            new AgendaRequest(inicio, Terca | Quinta, null, true)));

        var salvo = await db.Tratamentos.AsNoTracking().Include(t => t.Sessoes).SingleAsync(t => t.Id == id);
        var horizonte = AgendaDeSessoes.HorizonteContinuo(inicio, FusoBrasilia.HojeEmBrasilia());
        Assert.True(salvo.Continuo);
        Assert.Null(salvo.QuantidadeSessoes);
        Assert.Equal(horizonte, salvo.SessoesGeradasAte);
        Assert.Equal(AgendaDeSessoes.GerarAte(inicio, Terca | Quinta, horizonte).Count, salvo.Sessoes.Count);
        Assert.True(salvo.Sessoes.Max(s => s.DataPrevista) <= horizonte);
    }

    [Fact]
    public async Task Sem_tipo_e_recusado_porque_o_tempo_medio_vem_dele()
    {
        await using var db = fixture.CriarDbContext();
        var destino = await NovoDestinoAsync(db);

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db).CadastrarAsync(Novo(destino, Guid.Empty) with { TipoTratamentoId = null }));
    }

    [Fact]
    public async Task Segundo_acompanhante_sem_justificativa_e_recusado_e_com_ela_carimba_quem_liberou()
    {
        await using var db = fixture.CriarDbContext();
        var destino = await NovoDestinoAsync(db);
        var tipo = await NovoTipoAsync(db);
        var usuario = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db, usuarioId: usuario).CadastrarAsync(Novo(destino, tipo, acompanhantes: new RegraAcompanhantesRequest(2, "  "))));

        var id = await Servico(db, usuarioId: usuario).CadastrarAsync(
            Novo(destino, tipo, acompanhantes: new RegraAcompanhantesRequest(2, "Paciente acamado, precisa de dois para o embarque")));

        var salvo = await db.Tratamentos.AsNoTracking().SingleAsync(t => t.Id == id);
        Assert.Equal(2, salvo.QuantidadeAcompanhantes);
        Assert.Equal(usuario, salvo.SegundoAcompanhanteLiberadoPor);
        Assert.NotNull(salvo.SegundoAcompanhanteLiberadoEm);
    }

    [Fact]
    public async Task Alterar_agenda_refaz_so_as_pendentes_e_preserva_realizada_e_confirmada()
    {
        await using var db = fixture.CriarDbContext();
        var inicio = Proxima(DayOfWeek.Monday);
        var id = await Servico(db).CadastrarAsync(Novo(await NovoDestinoAsync(db), await NovoTipoAsync(db),
            new AgendaRequest(inicio, Segunda | Quarta, 6, false)));

        // Duas primeiras já têm desfecho; as outras quatro seguem pendentes.
        var sessoes = await db.Sessoes.Where(s => s.TratamentoId == id).OrderBy(s => s.DataPrevista).ToListAsync();
        sessoes[0].Status = StatusSessao.Realizada;
        sessoes[1].Status = StatusSessao.Confirmada;
        await db.SaveChangesAsync();

        // A partir da semana seguinte, passa para terça e quinta, mantendo 6 no total.
        var novoInicio = inicio.AddDays(7);
        await Servico(db).AlterarAgendaAsync(id, new AgendaRequest(novoInicio, Terca | Quinta, 6, false));

        await using var leitura = fixture.CriarDbContext();
        var depois = await leitura.Sessoes.AsNoTracking().Where(s => s.TratamentoId == id).OrderBy(s => s.DataPrevista).ToListAsync();
        Assert.Equal(6, depois.Count);
        Assert.Contains(depois, s => s.Id == sessoes[0].Id && s.Status == StatusSessao.Realizada);
        Assert.Contains(depois, s => s.Id == sessoes[1].Id && s.Status == StatusSessao.Confirmada);
        Assert.All(depois.Where(s => s.DataPrevista >= novoInicio),
            s => Assert.Contains(s.DataPrevista.DayOfWeek, new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday }));
        var t = await leitura.Tratamentos.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(Terca | Quinta, t.DiasSemanaMascara);
        Assert.Equal(novoInicio, t.DataInicio);
    }

    [Fact]
    public async Task Alterar_agenda_para_o_passado_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var id = await Servico(db).CadastrarAsync(Novo(await NovoDestinoAsync(db), await NovoTipoAsync(db)));

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db).AlterarAgendaAsync(id,
            new AgendaRequest(FusoBrasilia.HojeEmBrasilia().AddDays(-1), Segunda, 3, false)));
    }

    [Fact]
    public async Task Encerrar_cancela_as_viagens_futuras_que_nao_estao_numa_rota()
    {
        await using var db = fixture.CriarDbContext();
        var id = await Servico(db).CadastrarAsync(Novo(await NovoDestinoAsync(db), await NovoTipoAsync(db),
            new AgendaRequest(Proxima(DayOfWeek.Monday), Segunda | Quinta, 4, false)));

        await Servico(db).EncerrarAsync(id);

        await using var leitura = fixture.CriarDbContext();
        var t = await leitura.Tratamentos.AsNoTracking().Include(x => x.Sessoes).SingleAsync(x => x.Id == id);
        Assert.False(t.Ativo);
        Assert.All(t.Sessoes, s => Assert.Equal(StatusSessao.Cancelada, s.Status));
    }

    [Fact]
    public async Task Renovar_continuo_estende_o_horizonte_e_e_idempotente()
    {
        await using var db = fixture.CriarDbContext();
        var inicio = Proxima(DayOfWeek.Monday);
        var id = await Servico(db).CadastrarAsync(Novo(await NovoDestinoAsync(db), await NovoTipoAsync(db),
            new AgendaRequest(inicio, Segunda, null, true)));
        var antes = (await db.Tratamentos.AsNoTracking().SingleAsync(t => t.Id == id)).SessoesGeradasAte!.Value;

        // Dois meses adiante (o início pode já cair no mês que vem): o horizonte anda e só entram dias novos.
        var adiante = FusoBrasilia.HojeEmBrasilia().AddMonths(2);
        var pacientes = Substitute.For<IPacientesService>();
        var renovados = await Servico(db, pacientes).RenovarContinuosAsync(adiante);
        Assert.True(renovados >= 1);

        await using var leitura = fixture.CriarDbContext();
        var t = await leitura.Tratamentos.AsNoTracking().Include(x => x.Sessoes).SingleAsync(x => x.Id == id);
        var esperado = AgendaDeSessoes.HorizonteContinuo(inicio, adiante);
        Assert.True(esperado > antes);
        Assert.Equal(esperado, t.SessoesGeradasAte);
        Assert.Equal(t.Sessoes.Count, t.Sessoes.Select(s => s.DataPrevista).Distinct().Count());
        Assert.Equal(AgendaDeSessoes.GerarAte(inicio, Segunda, esperado).Count, t.Sessoes.Count);

        // Rodar de novo no mesmo dia não gera nada.
        var quantas = t.Sessoes.Count;
        await Servico(db, pacientes).RenovarContinuosAsync(adiante);
        Assert.Equal(quantas, await leitura.Sessoes.CountAsync(s => s.TratamentoId == id));
    }

    [Fact]
    public async Task Renovar_continuo_pula_paciente_com_obito()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var id = await Servico(db).CadastrarAsync(Novo(await NovoDestinoAsync(db), await NovoTipoAsync(db),
            new AgendaRequest(Proxima(DayOfWeek.Monday), Segunda, null, true), pacienteId: pacienteId));
        var antes = (await db.Tratamentos.AsNoTracking().SingleAsync(t => t.Id == id)).SessoesGeradasAte;

        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(pacienteId, Arg.Any<CancellationToken>())
            .Returns(PacienteDtoFabrica.Criar(pacienteId, "Paciente") with { DataObito = FusoBrasilia.HojeEmBrasilia() });

        await Servico(db, pacientes).RenovarContinuosAsync(FusoBrasilia.HojeEmBrasilia().AddMonths(2));

        await using var leitura = fixture.CriarDbContext();
        Assert.Equal(antes, (await leitura.Tratamentos.AsNoTracking().SingleAsync(t => t.Id == id)).SessoesGeradasAte);
    }

    [Fact]
    public async Task Viagem_aceita_ate_o_limite_e_so_acompanhante_da_lista_do_paciente()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var id = await Servico(db).CadastrarAsync(Novo(await NovoDestinoAsync(db), await NovoTipoAsync(db), pacienteId: pacienteId));
        var sessaoId = (await db.Sessoes.AsNoTracking().FirstAsync(s => s.TratamentoId == id)).Id;

        var mae = NovoAcompanhante(pacienteId, "52998224725");
        var pai = NovoAcompanhante(pacienteId, "11144477735");
        var deOutro = NovoAcompanhante(Guid.NewGuid(), "12345678909");
        db.Acompanhantes.AddRange(mae, pai, deOutro);
        await db.SaveChangesAsync();

        // Limite 1: dois é recusado.
        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db).DefinirAcompanhantesDaSessaoAsync(id, sessaoId,
            new DefinirAcompanhantesSessaoRequest([mae.Id, pai.Id])));
        // De outro paciente: recusado.
        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db).DefinirAcompanhantesDaSessaoAsync(id, sessaoId,
            new DefinirAcompanhantesSessaoRequest([deOutro.Id])));

        await Servico(db).DefinirAcompanhantesDaSessaoAsync(id, sessaoId, new DefinirAcompanhantesSessaoRequest([mae.Id]));

        var dto = await Servico(db).ObterPorIdAsync(id);
        var sessao = Assert.Single(dto.Sessoes, s => s.Id == sessaoId);
        Assert.Equal(mae.Id, Assert.Single(sessao.Acompanhantes).Id);
        await using var leitura = fixture.CriarDbContext();
        Assert.True((await leitura.Sessoes.AsNoTracking().SingleAsync(s => s.Id == sessaoId)).AcompanhanteEsperado);
    }

    private static Acompanhante NovoAcompanhante(Guid pacienteId, string cpf) => new()
    {
        Id = Guid.CreateVersion7(),
        PacienteId = pacienteId,
        Cpf = cpf,
        Nome = $"ACOMPANHANTE {cpf}",
        DataNascimento = new DateOnly(1970, 1, 1),
        FonteNome = FonteNomeAcompanhante.Base,
        Origem = OrigemCadastroAcompanhante.Painel,
        CriadoEm = DateTime.UtcNow,
    };
}
