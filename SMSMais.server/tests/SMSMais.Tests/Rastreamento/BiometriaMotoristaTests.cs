using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Motoristas;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Rastreamento.Biometria;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Rastreamento;

/// <summary>
/// Login do motorista no tablet pelo rosto (ADR-0073): só entra quem está escalado hoje no veículo do
/// tablet; o primeiro acesso passa pelo código no WhatsApp e grava a foto de referência uma vez só; a
/// central libera com código de uso único; cada conferência entra uma vez na trilha e a selfie sai na
/// retenção.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class BiometriaMotoristaTests(PostgresFixture fixture)
{
    private readonly IWhatsAppCliente _whatsapp = Substitute.For<IWhatsAppCliente>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    private BiometriaTabletService Tablet(SmsMaisDbContext db, bool modoTeste = true) =>
        new(db, _whatsapp, _cache, Config(modoTeste), NullLogger<BiometriaTabletService>.Instance);

    private static BiometriaPainelService Painel(SmsMaisDbContext db, Guid? usuarioId = null) =>
        new(db, new UsuarioAtualAccessorFake(usuarioId));

    private static IConfiguration Config(bool modoTeste) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Tfd:Otp:ModoTeste"] = modoTeste ? "true" : "false" })
        .Build();

    private static string FotoJpeg() => Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5]);

    private sealed record Cenario(DispositivoVeiculo Tablet, Guid MotoristaId, string Cpf, Guid RotaId, string Nome);

    private static string NovoCpf() => Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    private static async Task<DispositivoVeiculo> TabletAsync(SmsMaisDbContext db)
    {
        var agora = DateTime.UtcNow;
        var veiculo = new Veiculo
        {
            Id = Guid.NewGuid(),
            Placa = $"B{Random.Shared.Next(100000, 999999)}",
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

    private static async Task<(Guid Id, string Cpf, string Nome)> MotoristaAsync(
        SmsMaisDbContext db, string? telefone = "(21) 98765-4321")
    {
        var agora = DateTime.UtcNow;
        var cpf = NovoCpf();
        var nome = $"Motorista {cpf[..4]}";
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = nome,
            Cpf = cpf,
            Telefone = telefone,
            SenhaHash = "PENDENTE_AUTH",
            CriadoEm = agora,
        };
        var motorista = new Motorista { Id = Guid.NewGuid(), UsuarioId = usuario.Id, Cnh = cpf, CriadoEm = agora };
        db.Usuarios.Add(usuario);
        db.Motoristas.Add(motorista);
        await db.SaveChangesAsync();
        return (motorista.Id, cpf, nome);
    }

    private static async Task<Guid> RotaAsync(
        SmsMaisDbContext db, Guid veiculoId, Guid motoristaId, DateOnly? dia = null,
        StatusRota status = StatusRota.Planejada)
    {
        var rota = new RotaDiaria
        {
            Id = Guid.NewGuid(),
            Data = dia ?? FusoBrasilia.HojeEmBrasilia(),
            VeiculoId = veiculoId,
            MotoristaId = motoristaId,
            Status = status,
            CriadoEm = DateTime.UtcNow,
        };
        db.Rotas.Add(rota);
        await db.SaveChangesAsync();
        return rota.Id;
    }

    private static async Task<Cenario> EscaladoHojeAsync(SmsMaisDbContext db, string? telefone = "(21) 98765-4321")
    {
        var tablet = await TabletAsync(db);
        var (id, cpf, nome) = await MotoristaAsync(db, telefone);
        var rota = await RotaAsync(db, tablet.VeiculoId, id);
        return new Cenario(tablet, id, cpf, rota, nome);
    }

    /// <summary>CPF → código (modo teste devolve) → confirma → manda a foto.</summary>
    private async Task<ReferenciaCadastradaDto> PrimeiroAcessoAsync(SmsMaisDbContext db, Cenario c)
    {
        var servico = Tablet(db);
        var codigo = await servico.SolicitarCodigoAsync(c.Tablet, new SolicitarCodigoMotoristaRequest(c.Cpf));
        var liberado = await servico.ConfirmarCodigoAsync(
            c.Tablet, new ConfirmarCodigoMotoristaRequest(c.Cpf, codigo.Codigo!));
        return await servico.CadastrarRostoAsync(
            c.Tablet, new CadastrarRostoRequest(liberado.Ticket, FotoJpeg(), liberado.Termo.Versao, -22.9, -42.8));
    }

    [Fact]
    public async Task Escalados_do_dia_so_trazem_as_rotas_de_hoje_do_proprio_veiculo()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        var hoje = FusoBrasilia.HojeEmBrasilia();

        var (amanha, _, _) = await MotoristaAsync(db);
        await RotaAsync(db, c.Tablet.VeiculoId, amanha, hoje.AddDays(1));
        var (cancelado, _, _) = await MotoristaAsync(db);
        await RotaAsync(db, c.Tablet.VeiculoId, cancelado, status: StatusRota.Cancelada);
        var (voltaDeOntem, _, _) = await MotoristaAsync(db);
        await RotaAsync(db, c.Tablet.VeiculoId, voltaDeOntem, hoje.AddDays(-1), StatusRota.EmAndamento);
        var (outroVeiculo, _, _) = await MotoristaAsync(db);
        await RotaAsync(db, (await TabletAsync(db)).VeiculoId, outroVeiculo);
        await RotaAsync(db, c.Tablet.VeiculoId, c.MotoristaId); // segunda rota do mesmo motorista

        var escalados = await Tablet(db).EscaladosDoDiaAsync(c.Tablet);

        Assert.Equal(
            new[] { c.MotoristaId, voltaDeOntem }.Order(),
            escalados.Select(e => e.MotoristaId).Order());
        var meu = escalados.Single(e => e.MotoristaId == c.MotoristaId);
        Assert.Equal(c.Nome, meu.Nome);
        Assert.False(meu.TemReferencia);
    }

    [Fact]
    public async Task Primeiro_acesso_grava_a_referencia_com_o_termo_e_entra_na_trilha()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);

        var codigo = await Tablet(db).SolicitarCodigoAsync(c.Tablet, new SolicitarCodigoMotoristaRequest(c.Cpf));
        Assert.True(codigo.Enviado);
        Assert.Equal("tela-teste", codigo.Canal);
        Assert.Equal("***-4321", codigo.DestinoMascarado);

        var liberado = await Tablet(db).ConfirmarCodigoAsync(
            c.Tablet, new ConfirmarCodigoMotoristaRequest(c.Cpf, codigo.Codigo!));
        Assert.Equal(c.MotoristaId, liberado.MotoristaId);
        Assert.Equal(TermoBiometria.Versao, liberado.Termo.Versao);

        var cadastrada = await Tablet(db).CadastrarRostoAsync(
            c.Tablet, new CadastrarRostoRequest(liberado.Ticket, FotoJpeg(), TermoBiometria.Versao, -22.9, -42.8));

        var salva = await db.ReferenciasFaciaisMotorista.AsNoTracking().SingleAsync(r => r.MotoristaId == c.MotoristaId);
        Assert.Equal(cadastrada.FotoHash, salva.FotoHash);
        Assert.Equal(64, salva.FotoHash.Length);
        Assert.Equal(TermoBiometria.Hash, salva.TermoHash);
        Assert.Equal(c.Tablet.VeiculoId, salva.VeiculoId);

        var escalado = Assert.Single(await Tablet(db).EscaladosDoDiaAsync(c.Tablet));
        Assert.True(escalado.TemReferencia);
        Assert.Equal(cadastrada.FotoHash, escalado.FotoHash);
        Assert.Equal(FotoJpeg(), (await Tablet(db).ReferenciaAsync(c.Tablet, c.MotoristaId)).FotoBase64);

        var trilha = await db.VerificacoesFaciaisMotorista.AsNoTracking().SingleAsync(v => v.MotoristaId == c.MotoristaId);
        Assert.Equal(MomentoVerificacaoFacial.PrimeiroAcesso, trilha.Momento);
        Assert.True(trilha.Aprovada);
    }

    [Fact]
    public async Task Sem_modo_teste_o_codigo_vai_pelo_whatsapp_e_nao_volta_na_resposta()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        _whatsapp
            .EnviarTemplateAutenticacaoAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Guid?>(), Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>())
            .Returns(new EnvioWhatsAppResultado(true, "wamid.1", null));

        var r = await Tablet(db, modoTeste: false)
            .SolicitarCodigoAsync(c.Tablet, new SolicitarCodigoMotoristaRequest(c.Cpf));

        Assert.Equal("whatsapp", r.Canal);
        Assert.Null(r.Codigo);
        await _whatsapp.Received(1).EnviarTemplateAutenticacaoAsync(
            "21987654321", "authzap", "pt_BR", Arg.Any<string>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>(), Arg.Any<OrigemEnvioWhatsApp>());
    }

    [Fact]
    public async Task Primeiro_acesso_nao_sai_para_quem_nao_esta_escalado_sem_telefone_ou_ja_cadastrado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        var (naoEscalado, cpfNaoEscalado, _) = await MotoristaAsync(db);
        Assert.NotEqual(Guid.Empty, naoEscalado);

        var semTelefone = await TabletAsync(db);
        var (idSemTelefone, cpfSemTelefone, _) = await MotoristaAsync(db, telefone: null);
        await RotaAsync(db, semTelefone.VeiculoId, idSemTelefone);

        Assert.Equal(SituacaoPrimeiroAcesso.NaoEscalado, (await Tablet(db)
            .SolicitarCodigoAsync(c.Tablet, new SolicitarCodigoMotoristaRequest(cpfNaoEscalado))).Situacao);
        Assert.Equal(SituacaoPrimeiroAcesso.SemTelefone, (await Tablet(db)
            .SolicitarCodigoAsync(semTelefone, new SolicitarCodigoMotoristaRequest(cpfSemTelefone))).Situacao);

        await PrimeiroAcessoAsync(db, c);
        _cache.Remove($"otp:motorista:intervalo:{c.Cpf}");
        Assert.Equal(SituacaoPrimeiroAcesso.JaCadastrado, (await Tablet(db)
            .SolicitarCodigoAsync(c.Tablet, new SolicitarCodigoMotoristaRequest(c.Cpf))).Situacao);
    }

    [Fact]
    public async Task Codigo_errado_cinco_vezes_ou_de_outro_tablet_nao_vale()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        var codigo = await Tablet(db).SolicitarCodigoAsync(c.Tablet, new SolicitarCodigoMotoristaRequest(c.Cpf));

        var outro = await TabletAsync(db);
        var deOutro = await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
            .ConfirmarCodigoAsync(outro, new ConfirmarCodigoMotoristaRequest(c.Cpf, codigo.Codigo!)));
        Assert.True(deOutro.Erros.ContainsKey("codigo.expirado"));

        var errado = codigo.Codigo == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
                .ConfirmarCodigoAsync(c.Tablet, new ConfirmarCodigoMotoristaRequest(c.Cpf, errado)));
        }
        var depois = await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
            .ConfirmarCodigoAsync(c.Tablet, new ConfirmarCodigoMotoristaRequest(c.Cpf, codigo.Codigo!)));
        Assert.True(depois.Erros.ContainsKey("codigo.expirado"));

        var deNovo = await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
            .SolicitarCodigoAsync(c.Tablet, new SolicitarCodigoMotoristaRequest(c.Cpf)));
        Assert.True(deNovo.Erros.ContainsKey("codigo.aguarde"));
    }

    [Fact]
    public async Task Foto_de_referencia_so_vai_para_o_tablet_do_veiculo_escalado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        await PrimeiroAcessoAsync(db, c);
        var outro = await TabletAsync(db);

        await Assert.ThrowsAsync<NaoEncontradoException>(() => Tablet(db).ReferenciaAsync(outro, c.MotoristaId));
    }

    [Fact]
    public async Task Conferencia_entra_uma_vez_e_selfie_invalida_nao_derruba()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        var idLocal = Guid.NewGuid();
        RegistrarVerificacaoFacialRequest Pedido(string? selfie) => new(
            idLocal, c.MotoristaId, MomentoVerificacaoFacial.Login, true, 0.91, 0.62, true, "mobilefacenet-v1",
            0, 0, 5, c.RotaId, DateTime.UtcNow.AddHours(3), selfie);

        var primeira = await Tablet(db).RegistrarVerificacaoAsync(c.Tablet, Pedido("não é jpeg"));
        var reenvio = await Tablet(db).RegistrarVerificacaoAsync(c.Tablet, Pedido(FotoJpeg()));

        Assert.Equal(primeira.Id, reenvio.Id);
        var salva = await db.VerificacoesFaciaisMotorista.AsNoTracking().SingleAsync(v => v.IdLocal == idLocal);
        Assert.Null(salva.SelfieBase64);
        Assert.Null(salva.Latitude); // 0,0 = GPS sem fix
        Assert.True(salva.CapturadaEm <= DateTime.UtcNow.AddMinutes(1)); // relógio adiantado do tablet
        Assert.Equal(0.91, salva.Nota);
    }

    [Fact]
    public async Task Liberacao_da_central_entra_uma_vez_e_cai_depois_de_tentativas_erradas()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        var central = Guid.NewGuid();

        var gerada = await Painel(db, central).GerarLiberacaoAsync(
            c.Tablet.VeiculoId, new GerarLiberacaoRequest(c.MotoristaId));
        var errado = gerada.Codigo == "000000" ? "111111" : "000000";
        await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
            .UsarLiberacaoAsync(c.Tablet, new UsarLiberacaoRequest(Guid.NewGuid(), errado, null, null)));

        var idLocal = Guid.NewGuid();
        var entrou = await Tablet(db).UsarLiberacaoAsync(
            c.Tablet, new UsarLiberacaoRequest(idLocal, gerada.Codigo, -22.9, -42.8));
        Assert.Equal(c.MotoristaId, entrou.MotoristaId);

        // Reenvio do mesmo pedido (rede caiu na resposta) devolve o mesmo motorista; outro pedido, não.
        Assert.Equal(c.MotoristaId, (await Tablet(db).UsarLiberacaoAsync(
            c.Tablet, new UsarLiberacaoRequest(idLocal, gerada.Codigo, null, null))).MotoristaId);
        await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
            .UsarLiberacaoAsync(c.Tablet, new UsarLiberacaoRequest(Guid.NewGuid(), gerada.Codigo, null, null)));

        var trilha = Assert.Single(await Painel(db).ListarVerificacoesAsync(
            new FiltroVerificacoesFaciais(c.Tablet.VeiculoId, null, null)));
        Assert.Equal(MeioVerificacaoMotorista.CodigoCentral, trilha.Meio);

        var segunda = await Painel(db, central).GerarLiberacaoAsync(
            c.Tablet.VeiculoId, new GerarLiberacaoRequest(c.MotoristaId));
        var errado2 = segunda.Codigo == "000000" ? "111111" : "000000";
        for (var i = 0; i < BiometriaTabletService.MaxTentativasLiberacao; i++)
        {
            await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
                .UsarLiberacaoAsync(c.Tablet, new UsarLiberacaoRequest(Guid.NewGuid(), errado2, null, null)));
        }
        await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
            .UsarLiberacaoAsync(c.Tablet, new UsarLiberacaoRequest(Guid.NewGuid(), segunda.Codigo, null, null)));
    }

    [Fact]
    public async Task Gerar_outra_liberacao_anula_a_anterior_do_mesmo_veiculo()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);

        var primeira = await Painel(db).GerarLiberacaoAsync(c.Tablet.VeiculoId, new GerarLiberacaoRequest(c.MotoristaId));
        var segunda = await Painel(db).GerarLiberacaoAsync(c.Tablet.VeiculoId, new GerarLiberacaoRequest(c.MotoristaId));

        if (primeira.Codigo != segunda.Codigo)
        {
            await Assert.ThrowsAsync<ValidacaoException>(() => Tablet(db)
                .UsarLiberacaoAsync(c.Tablet, new UsarLiberacaoRequest(Guid.NewGuid(), primeira.Codigo, null, null)));
        }
        Assert.Equal(c.MotoristaId, (await Tablet(db).UsarLiberacaoAsync(
            c.Tablet, new UsarLiberacaoRequest(Guid.NewGuid(), segunda.Codigo, null, null))).MotoristaId);
    }

    [Fact]
    public async Task Apagar_no_painel_ou_excluir_o_motorista_tira_a_foto_do_rosto()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        await PrimeiroAcessoAsync(db, c);
        Assert.True((await Painel(db).SituacaoAsync(c.MotoristaId)).Cadastrada);

        await Painel(db).ApagarAsync(c.MotoristaId);
        Assert.False((await Painel(db).SituacaoAsync(c.MotoristaId)).Cadastrada);

        _cache.Remove($"otp:motorista:intervalo:{c.Cpf}");
        await PrimeiroAcessoAsync(db, c);
        await new MotoristasService(db, new UsuarioAtualAccessorFake()).DesativarAsync(c.MotoristaId);
        Assert.False(await db.ReferenciasFaciaisMotorista.AnyAsync(r => r.MotoristaId == c.MotoristaId));
    }

    [Fact]
    public async Task Retencao_apaga_a_selfie_vencida_e_mantem_o_registro()
    {
        await using var db = fixture.CriarDbContext();
        var c = await EscaladoHojeAsync(db);
        var antiga = await Tablet(db).RegistrarVerificacaoAsync(c.Tablet, new RegistrarVerificacaoFacialRequest(
            Guid.NewGuid(), c.MotoristaId, MomentoVerificacaoFacial.Login, true, 0.9, 0.62, true, null,
            null, null, null, null, DateTime.UtcNow, FotoJpeg()));
        var recente = await Tablet(db).RegistrarVerificacaoAsync(c.Tablet, new RegistrarVerificacaoFacialRequest(
            Guid.NewGuid(), c.MotoristaId, MomentoVerificacaoFacial.Login, true, 0.9, 0.62, true, null,
            null, null, null, null, DateTime.UtcNow, FotoJpeg()));
        await db.VerificacoesFaciaisMotorista.Where(v => v.Id == antiga.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.RecebidaEm, DateTime.UtcNow.AddDays(-91)));

        await LimpezaSelfiesWorker.ApagarVencidasAsync(db, 90, DateTime.UtcNow, CancellationToken.None);

        var velha = await db.VerificacoesFaciaisMotorista.AsNoTracking().SingleAsync(v => v.Id == antiga.Id);
        Assert.Null(velha.SelfieBase64);
        Assert.NotNull(velha.SelfieApagadaEm);
        Assert.Equal(FotoJpeg(), (await Painel(db).SelfieAsync(recente.Id)).FotoBase64);
        await Assert.ThrowsAsync<NaoEncontradoException>(() => Painel(db).SelfieAsync(antiga.Id));
    }
}
