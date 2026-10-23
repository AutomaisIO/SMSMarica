using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Faturamento;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Translado;
using SMSMais.Core.Translado.Localizacao;
using SMSMais.Core.Translado.Viagem;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Translado;

/// <summary>
/// Localização do paciente no dia da viagem (ADR-0079): quando ele pode compartilhar, quem vê, quando a
/// posição é apagada e as travas de posse. Pacientes fictícios (nada de dado real).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class CompartilhamentoLocalizacaoServiceTests(PostgresFixture fixture)
{
    private readonly IAuditoriaService _auditoria = Substitute.For<IAuditoriaService>();

    private CompartilhamentoLocalizacaoService Servico(SmsMaisDbContext db) => new(db, _auditoria);

    private static readonly Guid SessaoApp = Guid.NewGuid();

    // ------------------------------------------------------------------------------------- a espera

    [Fact]
    public async Task Sem_viagem_hoje_nao_ha_espera()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        await SessaoAsync(db, c, Guid.NewGuid(), dia: FusoBrasilia.HojeEmBrasilia().AddDays(1), busca: Agora().AddHours(1));

        (await Servico(db).DoPacienteAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task Ida_abre_2h_antes_da_busca_compartilha_e_apaga_no_embarque()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, c, pacienteId, busca: Agora().AddHours(1));
        var rotaId = await RotaAsync(db, c, StatusRota.Planejada);
        await AlocarAsync(db, rotaId, sessaoId, c.Assento, TipoParada.Coleta);
        var servico = Servico(db);

        var espera = await servico.DoPacienteAsync(pacienteId);
        espera.Should().NotBeNull();
        espera!.Trecho.Should().Be(TrechoViagem.Ida);
        espera.Compartilhando.Should().BeFalse();

        var iniciado = await servico.IniciarAsync(pacienteId, SessaoApp, "nativo", sessaoId);
        iniciado.Compartilhando.Should().BeTrue();
        await _auditoria.Received(1).RegistrarAsync(
            Arg.Is(CompartilhamentoLocalizacaoService.EntidadeAuditoria), Arg.Is(sessaoId.ToString()), Arg.Is("Iniciado"),
            Arg.Is<string?>(v => v == null), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        await servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.92, -42.81, precisao: 12));
        var noTablet = await servico.DoTabletAsync(c.Tablet);
        noTablet.Should().ContainSingle(p => p.SessaoId == sessaoId && p.Trecho == TrechoViagem.Ida
            && p.Latitude == -22.92 && p.PrecisaoM == 12);

        // O motorista marca o embarque no tablet: a posição sai junto com a marcação.
        await Viagem(db).RegistrarDoTabletAsync(c.Tablet, new RegistrarEventoViagemRequest(
            Guid.NewGuid(), rotaId, TipoEventoViagem.Embarque, sessaoId, PessoaViagem.Paciente, null, null,
            c.MotoristaId, DateTime.UtcNow, -22.92, -42.81, 10));

        await using var leitura = fixture.CriarDbContext();
        var linha = await leitura.CompartilhamentosLocalizacao.SingleAsync(x => x.SessaoId == sessaoId);
        linha.EncerradoEm.Should().NotBeNull();
        linha.MotivoEncerramento.Should().Be(MotivoFimCompartilhamento.Embarque);
        linha.Latitude.Should().BeNull();
        linha.Longitude.Should().BeNull();
        (await Servico(leitura).DoTabletAsync(c.Tablet)).Should().NotContain(p => p.SessaoId == sessaoId);
        (await Servico(leitura).DoPacienteAsync(pacienteId)).Should().BeNull("embarcou: não espera mais");
    }

    [Fact]
    public async Task Rota_planejada_sem_hora_de_busca_so_abre_quando_a_rota_comeca()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, c, pacienteId, busca: null);
        var rotaId = await RotaAsync(db, c, StatusRota.Planejada);
        await AlocarAsync(db, rotaId, sessaoId, c.Assento, TipoParada.Coleta);

        (await Servico(db).DoPacienteAsync(pacienteId)).Should().BeNull();

        var rota = await db.Rotas.SingleAsync(r => r.Id == rotaId);
        rota.Status = StatusRota.EmAndamento;
        await db.SaveChangesAsync();

        (await Servico(db).DoPacienteAsync(pacienteId))!.Trecho.Should().Be(TrechoViagem.Ida);
    }

    [Fact]
    public async Task Resgate_abre_depois_de_descer_na_unidade_e_so_o_carro_que_marcou_ve()
    {
        await using var db = fixture.CriarDbContext();
        var ida = await CenarioAsync(db);
        var volta = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, ida, pacienteId, status: StatusSessao.Realizada, chegouNaUnidade: true);
        var rotaIda = await RotaAsync(db, ida, StatusRota.EmAndamento);
        await AlocarAsync(db, rotaIda, sessaoId, ida.Assento, TipoParada.Coleta);
        var rotaVolta = await RotaAsync(db, volta, StatusRota.EmAndamento);
        await AlocarAsync(db, rotaVolta, sessaoId, volta.Assento, TipoParada.Retorno);
        var servico = Servico(db);

        var espera = await servico.DoPacienteAsync(pacienteId);
        espera!.Trecho.Should().Be(TrechoViagem.Resgate);
        await servico.IniciarAsync(pacienteId, SessaoApp, "pwa", sessaoId);
        await servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.90, -43.11));

        (await servico.DoTabletAsync(volta.Tablet)).Should().ContainSingle(p => p.SessaoId == sessaoId);
        (await servico.DoTabletAsync(ida.Tablet)).Should().NotContain(
            p => p.SessaoId == sessaoId, "o carro da ida não busca no resgate");
    }

    // ------------------------------------------------------------------------------------- as travas

    [Fact]
    public async Task Fora_da_espera_recusa_iniciar()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, c, pacienteId, busca: null);

        var acao = () => Servico(db).IniciarAsync(pacienteId, SessaoApp, "nativo", sessaoId);

        (await acao.Should().ThrowAsync<ConflitoException>()).Which.Codigo.Should().Be("localizacao.fora_da_janela");
    }

    [Fact]
    public async Task Outro_paciente_nao_envia_nem_inicia_na_viagem_alheia()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var (dono, intruso) = (Guid.NewGuid(), Guid.NewGuid());
        var sessaoId = await SessaoAsync(db, c, dono, busca: Agora().AddHours(1));
        var rotaId = await RotaAsync(db, c, StatusRota.EmAndamento);
        await AlocarAsync(db, rotaId, sessaoId, c.Assento, TipoParada.Coleta);
        var servico = Servico(db);
        await servico.IniciarAsync(dono, SessaoApp, "nativo", sessaoId);

        var enviar = () => servico.AtualizarAsync(intruso, sessaoId, Posicao(-22.0, -43.0));
        var iniciar = () => servico.IniciarAsync(intruso, SessaoApp, "nativo", sessaoId);

        (await enviar.Should().ThrowAsync<ConflitoException>()).Which.Codigo.Should().Be("localizacao.nao_iniciada");
        (await iniciar.Should().ThrowAsync<ConflitoException>()).Which.Codigo.Should().Be("localizacao.fora_da_janela");
        (await db.CompartilhamentosLocalizacao.AsNoTracking().SingleAsync(x => x.SessaoId == sessaoId))
            .Latitude.Should().BeNull("o intruso não mexeu na posição");
    }

    [Fact]
    public async Task Tablet_de_outro_veiculo_nao_ve_a_posicao()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var outro = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, c, pacienteId, busca: Agora().AddHours(1));
        var rotaId = await RotaAsync(db, c, StatusRota.EmAndamento);
        await AlocarAsync(db, rotaId, sessaoId, c.Assento, TipoParada.Coleta);
        var servico = Servico(db);
        await servico.IniciarAsync(pacienteId, SessaoApp, "nativo", sessaoId);
        await servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.92, -42.81));

        (await servico.DoTabletAsync(outro.Tablet)).Should().NotContain(p => p.SessaoId == sessaoId);
    }

    [Fact]
    public async Task Posicao_invalida_ou_antiga_e_recusada_e_relogio_adiantado_vira_agora()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, c, pacienteId, busca: Agora().AddHours(1));
        var rotaId = await RotaAsync(db, c, StatusRota.EmAndamento);
        await AlocarAsync(db, rotaId, sessaoId, c.Assento, TipoParada.Coleta);
        var servico = Servico(db);
        await servico.IniciarAsync(pacienteId, SessaoApp, "nativo", sessaoId);

        await FluentActions.Awaiting(() => servico.AtualizarAsync(pacienteId, sessaoId, Posicao(0, 0)))
            .Should().ThrowAsync<ValidacaoException>();
        await FluentActions.Awaiting(() => servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.9, -42.8, precisao: 9000)))
            .Should().ThrowAsync<ValidacaoException>();
        await FluentActions.Awaiting(() => servico.AtualizarAsync(
                pacienteId, sessaoId, Posicao(-22.9, -42.8) with { CapturadaEm = DateTime.UtcNow.AddMinutes(-10) }))
            .Should().ThrowAsync<ValidacaoException>();

        // O Postgres guarda microssegundos e o DateTime, frações menores: compara com folga.
        var noFuturo = DateTime.UtcNow.AddHours(1);
        await servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.9, -42.8) with { CapturadaEm = noFuturo });
        var linha = await db.CompartilhamentosLocalizacao.AsNoTracking().SingleAsync(x => x.SessaoId == sessaoId);
        linha.CapturadaEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30)).And.BeBefore(noFuturo);
    }

    [Fact]
    public async Task Rajada_mantem_a_posicao_anterior()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, c, pacienteId, busca: Agora().AddHours(1));
        var rotaId = await RotaAsync(db, c, StatusRota.EmAndamento);
        await AlocarAsync(db, rotaId, sessaoId, c.Assento, TipoParada.Coleta);
        var servico = Servico(db);
        await servico.IniciarAsync(pacienteId, SessaoApp, "nativo", sessaoId);

        await servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.91, -42.81));
        await servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.99, -42.99));

        (await db.CompartilhamentosLocalizacao.AsNoTracking().SingleAsync(x => x.SessaoId == sessaoId))
            .Latitude.Should().Be(-22.91);
    }

    // ---------------------------------------------------------------------------------------- o fim

    [Fact]
    public async Task Parar_apaga_a_posicao_e_registra_na_auditoria()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, c, pacienteId, busca: Agora().AddHours(1));
        var rotaId = await RotaAsync(db, c, StatusRota.EmAndamento);
        await AlocarAsync(db, rotaId, sessaoId, c.Assento, TipoParada.Coleta);
        var servico = Servico(db);
        await servico.IniciarAsync(pacienteId, SessaoApp, "nativo", sessaoId);
        await servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.92, -42.81));

        await servico.PararAsync(pacienteId, sessaoId);

        var linha = await db.CompartilhamentosLocalizacao.AsNoTracking().SingleAsync(x => x.SessaoId == sessaoId);
        linha.MotivoEncerramento.Should().Be(MotivoFimCompartilhamento.PeloPaciente);
        linha.Latitude.Should().BeNull();
        await _auditoria.Received(1).RegistrarAsync(
            Arg.Is(CompartilhamentoLocalizacaoService.EntidadeAuditoria), Arg.Is(sessaoId.ToString()), Arg.Is("Encerrado"),
            Arg.Is<string?>(v => v == null), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        (await servico.DoPacienteAsync(pacienteId))!.Compartilhando.Should().BeFalse("pode voltar a compartilhar");
    }

    [Fact]
    public async Task Rotina_encerra_quem_saiu_da_espera_sem_passar_pelo_tablet()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var pacienteId = Guid.NewGuid();
        var sessaoId = await SessaoAsync(db, c, pacienteId, busca: Agora().AddHours(1));
        var rotaId = await RotaAsync(db, c, StatusRota.EmAndamento);
        await AlocarAsync(db, rotaId, sessaoId, c.Assento, TipoParada.Coleta);
        var servico = Servico(db);
        await servico.IniciarAsync(pacienteId, SessaoApp, "nativo", sessaoId);
        await servico.AtualizarAsync(pacienteId, sessaoId, Posicao(-22.92, -42.81));

        // A central cancela a viagem no painel.
        var sessao = await db.Sessoes.SingleAsync(s => s.Id == sessaoId);
        sessao.Status = StatusSessao.Cancelada;
        await db.SaveChangesAsync();

        (await servico.EncerrarForaDaEsperaAsync()).Should().BeGreaterThanOrEqualTo(1);
        var linha = await db.CompartilhamentosLocalizacao.AsNoTracking().SingleAsync(x => x.SessaoId == sessaoId);
        linha.MotivoEncerramento.Should().Be(MotivoFimCompartilhamento.ForaDaJanela);
        linha.Latitude.Should().BeNull();
    }

    [Theory]
    [InlineData("07:30", "05:30")]
    [InlineData("02:00", "00:00")]
    [InlineData("01:15", "00:00")]
    public void Abertura_da_ida_e_2h_antes_sem_voltar_para_o_dia_anterior(string busca, string abertura) =>
        CompartilhamentoLocalizacaoService.AberturaDaIda(TimeOnly.Parse(busca)).Should().Be(TimeOnly.Parse(abertura));

    // ------------------------------------------------------------------------------------------ apoio

    private sealed record Cenario(DispositivoVeiculo Tablet, Guid MotoristaId, Assento Assento, Guid UnidadeId, Guid TipoId);

    private static TimeOnly Agora() => TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));

    private static AtualizarLocalizacaoRequest Posicao(double lat, double lng, double? precisao = 15) =>
        new(lat, lng, precisao, DateTime.UtcNow);

    private static ViagemService Viagem(SmsMaisDbContext db) => new(
        db, Substitute.For<IFaturamentoService>(), Substitute.For<IPacienteResolver>(),
        Substitute.For<IAvisoAoPacienteTransporte>(), NullLogger<ViagemService>.Instance);

    /// <summary>Veículo com tablet, motorista, um assento e a unidade de destino.</summary>
    private static async Task<Cenario> CenarioAsync(SmsMaisDbContext db)
    {
        var agora = DateTime.UtcNow;
        var veiculo = new Veiculo
        {
            Id = Guid.NewGuid(), Placa = $"L{Random.Shared.Next(100000, 999999)}",
            Modelo = "Spin", Fabricante = "Chevrolet", Cor = "Branca", CriadoEm = agora,
        };
        var fileira = new Fileira { Id = Guid.NewGuid(), VeiculoId = veiculo.Id, Ordem = 1, CriadoEm = agora };
        var assento = new Assento { Id = Guid.NewGuid(), FileiraId = fileira.Id, Numero = 1, CriadoEm = agora, Tipo = TipoAssento.Passageiro };
        var tablet = new DispositivoVeiculo
        {
            Id = Guid.NewGuid(), VeiculoId = veiculo.Id, TokenHash = Guid.NewGuid().ToString("N"),
            AtivadoEm = agora, CriadoEm = agora,
        };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(), NomeCompleto = "Motorista Simulado", SenhaHash = "PENDENTE_AUTH", CriadoEm = agora,
        };
        var motorista = new Motorista { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Cnh = Guid.NewGuid().ToString("N")[..11], CriadoEm = agora };
        var unidade = new UnidadeAtendimento
        {
            Id = Guid.NewGuid(), Nome = $"CLÍNICA SIMULADA {Guid.NewGuid():N}"[..30].ToUpperInvariant(),
            Endereco = new Endereco
            {
                Cep = "24900000", Logradouro = "Rua Simulada", Numero = "100", Bairro = "Centro",
                Cidade = "Niterói", Uf = "RJ",
            },
            Gps = new Gps(-22.89, -43.12), Externa = true, Ativo = true, CriadoEm = agora,
        };
        var tipo = new TipoTratamento
        {
            Id = Guid.NewGuid(), Nome = "Hemodiálise", Codigo = $"hemo_{Guid.NewGuid():N}",
            TempoMedioMinutos = 240, Ativo = true, CriadoEm = agora,
        };
        db.Veiculos.Add(veiculo);
        db.Fileiras.Add(fileira);
        db.Assentos.Add(assento);
        db.DispositivosVeiculo.Add(tablet);
        db.Usuarios.Add(usuario);
        db.Motoristas.Add(motorista);
        db.UnidadesAtendimento.Add(unidade);
        db.TiposTratamento.Add(tipo);
        await db.SaveChangesAsync();
        return new Cenario(tablet, motorista.Id, assento, unidade.Id, tipo.Id);
    }

    private static async Task<Guid> SessaoAsync(
        SmsMaisDbContext db, Cenario c, Guid pacienteId, TimeOnly? busca = null,
        StatusSessao status = StatusSessao.Pendente, DateOnly? dia = null, bool chegouNaUnidade = false)
    {
        var agora = DateTime.UtcNow;
        var data = dia ?? FusoBrasilia.HojeEmBrasilia();
        var tratamento = new Tratamento
        {
            Id = Guid.NewGuid(), PacienteId = pacienteId, UnidadeAtendimentoId = c.UnidadeId, TipoTratamentoId = c.TipoId,
            Descricao = "Atendimento simulado", DataInicio = data, DiasSemanaMascara = 127,
            Mobilidade = MobilidadeTransporte.Independente, Ativo = true, CriadoEm = agora,
        };
        var sessao = new SessaoDeTratamento
        {
            Id = Guid.NewGuid(), TratamentoId = tratamento.Id, DataPrevista = data, HoraPrevistaBusca = busca,
            Status = status, CriadoEm = agora,
            HoraSaidaResidencia = chegouNaUnidade ? new TimeOnly(6, 0) : null,
            HoraChegadaUnidade = chegouNaUnidade ? new TimeOnly(7, 0) : null,
        };
        db.Tratamentos.Add(tratamento);
        db.Sessoes.Add(sessao);
        await db.SaveChangesAsync();
        return sessao.Id;
    }

    private static async Task<Guid> RotaAsync(SmsMaisDbContext db, Cenario c, StatusRota status)
    {
        var rota = new RotaDiaria
        {
            Id = Guid.NewGuid(), Data = FusoBrasilia.HojeEmBrasilia(), VeiculoId = c.Tablet.VeiculoId,
            MotoristaId = c.MotoristaId, Status = status, CriadoEm = DateTime.UtcNow,
        };
        db.Rotas.Add(rota);
        await db.SaveChangesAsync();
        return rota.Id;
    }

    private static async Task AlocarAsync(SmsMaisDbContext db, Guid rotaId, Guid sessaoId, Assento assento, TipoParada parada)
    {
        db.Alocacoes.Add(new Alocacao
        {
            Id = Guid.NewGuid(), RotaDiariaId = rotaId, SessaoId = sessaoId, AssentoId = assento.Id,
            Tipo = TipoAlocacao.Paciente, Parada = parada, CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
