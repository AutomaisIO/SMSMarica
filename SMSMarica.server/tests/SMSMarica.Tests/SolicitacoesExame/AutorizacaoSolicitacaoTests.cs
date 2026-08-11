using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos.Assinatura;
using SMSMarica.Core.Notificacoes;
using SMSMarica.Core.Notificacoes.Comunicacao;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.SolicitacoesExame;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Core.Telefones;
using SMSMarica.Core.Worklist;
using SMSMarica.Core.Erros;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.SolicitacoesExame;

/// <summary>
/// Autorização presencial (recepção): gate de telefone verificado, régua da chave,
/// "presencial vence o cancelamento" e o gate do reenviar-worklist. Integração com
/// Postgres real (Testcontainers) — primeira suíte a usar a <see cref="PostgresFixture"/>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AutorizacaoSolicitacaoTests(PostgresFixture fixture)
{
    private SolicitacoesExameService CriarService(
        SmsMaricaDbContext db, string? cpfPaciente, Guid pacienteId, string? telefoneVerificado = null)
    {
        // O gate de autorização lê o verificado do Patient FHIR (via resolver) — não há mais
        // tabela contato_validado para semear; o resumo do mock carrega o telefone verificado.
        var resolver = Substitute.For<IPacienteResolver>();
        resolver.ResolverAsync(pacienteId, Arg.Any<CancellationToken>())
            .Returns(new PacienteResumo(
                pacienteId, "PACIENTE TESTE", cpfPaciente, null, null, Sexo.NaoInformado, telefoneVerificado));

        return new SolicitacoesExameService(
            db,
            Substitute.For<IGeradorIdentificadores>(),
            Substitute.For<IDcm4cheeMwlClient>(),
            new ResolvedorEstacaoWorklist(db, NullLogger<ResolvedorEstacaoWorklist>.Instance),
            Substitute.For<INotificadorExame>(),
            new UsuarioAtualAccessorFake(Guid.NewGuid()),
            resolver,
            Substitute.For<SMSMarica.Core.Pacientes.IPacientesService>(),
            // Serviço REAL de dispensa: o gate consulta o banco, e é isso que os testes de
            // dispensa exercitam (o mock esconderia justamente a leitura que importa).
            CriarDispensas(db),
            new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
            new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
            Substitute.For<IRegistroErroService>(),
            Substitute.For<SMSMarica.Core.Auditoria.IAuditoriaService>(),
            NullLogger<SolicitacoesExameService>.Instance);
    }

    /// <summary>Dispensa real sobre o banco de teste. O hub FHIR só é tocado no Registrar —
    /// aqui as dispensas são semeadas direto na tabela, então o cliente pode ser substitute.</summary>
    private static DispensaContatoService CriarDispensas(SmsMaricaDbContext db) =>
        new(db,
            Substitute.For<IPacienteFhirClient>(),
            new UsuarioAtualAccessorFake(Guid.NewGuid()),
            NullLogger<DispensaContatoService>.Instance);

    /// <summary>Semeia uma dispensa ATIVA para o paciente (o que a recepção teria registrado).</summary>
    private static async Task SemearDispensaAsync(
        SmsMaricaDbContext db, Guid pacienteId, MotivoDispensaContato motivo)
    {
        db.DispensasVerificacaoContato.Add(new DispensaVerificacaoContato
        {
            Id = Guid.CreateVersion7(),
            PacienteId = pacienteId,
            Motivo = motivo,
            PacienteCiente = true,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Autorizar_sem_numero_verificado_recusa()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var service = CriarService(db, SeedSolicitacao.CpfAleatorio(), pacienteId); // sem telefone verificado

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() => service.AutorizarAsync(s.Id, "12345"));
        Assert.Contains("verificado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Autorizar_sem_verificado_mas_com_dispensa_ativa_libera()
    {
        // Caso do balcão: paciente sem celular. A dispensa registrada é o que substitui o OTP —
        // sem ela, a recepção não tinha como liberar o exame e o paciente ficava parado.
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        await SemearDispensaAsync(db, pacienteId, MotivoDispensaContato.SemCelular);
        var service = CriarService(db, SeedSolicitacao.CpfAleatorio(), pacienteId); // sem telefone verificado

        await service.AutorizarAsync(s.Id, "12345");

        var atual = await db.ExamesImagem.Include(x => x.Solicitacao).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.NotNull(atual.Solicitacao!.AutorizadoEm);
    }

    [Fact]
    public async Task Autorizar_com_dispensa_revogada_volta_a_recusar()
    {
        // A dispensa é temporária por natureza (cai quando o contato é verificado ou o telefone
        // muda). Revogada, o gate tem de voltar a valer — senão a válvula viraria porta aberta.
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        await SemearDispensaAsync(db, pacienteId, MotivoDispensaContato.SemCelular);
        await CriarDispensas(db).RevogarAsync(pacienteId, "teste");
        var service = CriarService(db, SeedSolicitacao.CpfAleatorio(), pacienteId);

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() => service.AutorizarAsync(s.Id, "12345"));
        Assert.Contains("verificado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Theory]
    [InlineData("123")]     // abaixo do mínimo (9999)
    [InlineData("abc")]     // não numérico
    [InlineData("0001")]    // não é o sentinela
    public async Task Autorizar_com_chave_fora_da_regua_recusa(string chave)
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var cpf = SeedSolicitacao.CpfAleatorio();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var service = CriarService(db, cpf, pacienteId, telefoneVerificado: SeedSolicitacao.TelefoneAleatorio());

        await Assert.ThrowsAsync<ValidacaoException>(() => service.AutorizarAsync(s.Id, chave));
    }

    [Fact]
    public async Task Autorizar_pendente_confirma_presencial_e_enfileira_envio()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var cpf = SeedSolicitacao.CpfAleatorio();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId, enviarParaWorklist: true);
        var service = CriarService(db, cpf, pacienteId, telefoneVerificado: SeedSolicitacao.TelefoneAleatorio());

        await service.AutorizarAsync(s.Id, "12345");

        var atual = await db.ExamesImagem.Include(x => x.Solicitacao).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.NotNull(atual.Solicitacao!.AutorizadoEm);
        Assert.Equal("12345", atual.Solicitacao!.ChaveConfirmacao);
        Assert.Equal(StatusConfirmacaoAgendamento.Confirmada, atual.Solicitacao!.StatusConfirmacao);
        Assert.Equal("presencial", atual.Solicitacao!.ConfirmadoCanal);
        Assert.NotNull(atual.ProximaTentativaEm); // envio ao PACS liberado SÓ agora
    }

    [Fact]
    public async Task Autorizar_apos_cancelamento_revive_como_presencial()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var cpf = SeedSolicitacao.CpfAleatorio();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId, confirmacao: StatusConfirmacaoAgendamento.Cancelada);
        s.Solicitacao!.ConfirmacaoCanceladaEm = DateTime.UtcNow.AddHours(-2);
        s.Solicitacao!.MotivoCancelamentoPaciente = "vou viajar";
        await db.SaveChangesAsync();
        var service = CriarService(db, cpf, pacienteId, telefoneVerificado: SeedSolicitacao.TelefoneAleatorio());

        await service.AutorizarAsync(s.Id, "0000"); // sentinela extra-SUS também passa na régua

        var atual = await db.ExamesImagem.Include(x => x.Solicitacao).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(StatusConfirmacaoAgendamento.Confirmada, atual.Solicitacao!.StatusConfirmacao);
        Assert.Equal("presencial", atual.Solicitacao!.ConfirmadoCanal);
        Assert.Null(atual.Solicitacao!.ConfirmacaoCanceladaEm);      // cancelamento limpo — "trouxe à vida"
        Assert.Null(atual.Solicitacao!.MotivoCancelamentoPaciente);
    }

    [Fact]
    public async Task Autorizar_tipo_sem_worklist_nao_enfileira_envio()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var cpf = SeedSolicitacao.CpfAleatorio();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId, enviarParaWorklist: false);
        var service = CriarService(db, cpf, pacienteId, telefoneVerificado: SeedSolicitacao.TelefoneAleatorio());

        await service.AutorizarAsync(s.Id, "99999");

        var atual = await db.ExamesImagem.Include(x => x.Solicitacao).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.NotNull(atual.Solicitacao!.AutorizadoEm);
        Assert.Null(atual.ProximaTentativaEm);
    }

    [Fact]
    public async Task Reenviar_worklist_sem_autorizacao_recusa_quando_nunca_enviada()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId); // Solicitada, AutorizadoEm null
        var service = CriarService(db, null, pacienteId);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() => service.ReenviarWorklistAsync(s.Id));
        Assert.Equal("solicitacaoExame.nao_autorizada", ex.Codigo);
    }

    [Fact]
    public async Task Reenviar_worklist_autorizada_enfileira()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        s.Solicitacao!.AutorizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var service = CriarService(db, null, pacienteId);

        await service.ReenviarWorklistAsync(s.Id);

        var atual = await db.ExamesImagem.Include(x => x.Solicitacao).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.NotNull(atual.ProximaTentativaEm);
    }

    // ---- Escolha da estação na autorização ----

    private static async Task<Equipamento> SemearEquipamentoAsync(
        SmsMaricaDbContext db, Guid unidadeId, string nome, string ae)
    {
        var equipamento = new Equipamento
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            UnidadeId = unidadeId,
            ModalidadeDicom = ModalidadeDicom.MG, // o seed cria TipoExame MG
            IdentificadorDicom = ae,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Equipamentos.Add(equipamento);
        await db.SaveChangesAsync();
        return equipamento;
    }

    [Fact]
    public async Task Autorizar_com_duas_estacoes_e_sem_escolha_recusa()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var unidadeId = s.Solicitacao!.UnidadeExecutanteId;
        await SemearEquipamentoAsync(db, unidadeId, "Sala 1", "SALA_1");
        await SemearEquipamentoAsync(db, unidadeId, "Sala 2", "SALA_2");
        var service = CriarService(db, SeedSolicitacao.CpfAleatorio(), pacienteId, telefoneVerificado: "21999990000");

        var ex = await Assert.ThrowsAsync<ConflitoException>(() => service.AutorizarAsync(s.Id, "12345"));

        Assert.Equal("autorizacao.equipamento_obrigatorio", ex.Codigo);
        // Recusou ANTES de autorizar: o exame não pode ter ido para a fila do PACS.
        var atual = await db.ExamesImagem.Include(x => x.Solicitacao).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Null(atual.Solicitacao!.AutorizadoEm);
        Assert.Null(atual.ProximaTentativaEm);
    }

    [Fact]
    public async Task Autorizar_com_estacao_escolhida_grava_e_enfileira()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var unidadeId = s.Solicitacao!.UnidadeExecutanteId;
        await SemearEquipamentoAsync(db, unidadeId, "Sala 1", "SALA_1");
        var sala2 = await SemearEquipamentoAsync(db, unidadeId, "Sala 2", "SALA_2");
        var service = CriarService(db, SeedSolicitacao.CpfAleatorio(), pacienteId, telefoneVerificado: "21999990000");

        await service.AutorizarAsync(s.Id, "12345", sala2.Id);

        var atual = await db.ExamesImagem.Include(x => x.Solicitacao).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(sala2.Id, atual.EquipamentoId);
        Assert.NotNull(atual.Solicitacao!.AutorizadoEm);
        Assert.NotNull(atual.ProximaTentativaEm);
    }

    [Fact]
    public async Task Autorizar_com_estacao_de_outra_unidade_recusa()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var outra = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid()); // outra unidade
        var intrusa = await SemearEquipamentoAsync(db, outra.Solicitacao!.UnidadeExecutanteId, "Sala X", "SALA_X");
        await SemearEquipamentoAsync(db, s.Solicitacao!.UnidadeExecutanteId, "Sala 1", "SALA_1");
        var service = CriarService(db, SeedSolicitacao.CpfAleatorio(), pacienteId, telefoneVerificado: "21999990000");

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() => service.AutorizarAsync(s.Id, "12345", intrusa.Id));

        Assert.Contains("não atende esta unidade", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Autorizar_com_estacao_unica_grava_sozinho()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var unica = await SemearEquipamentoAsync(db, s.Solicitacao!.UnidadeExecutanteId, "Sala Única", "SALA_UNICA");
        var service = CriarService(db, SeedSolicitacao.CpfAleatorio(), pacienteId, telefoneVerificado: "21999990000");

        await service.AutorizarAsync(s.Id, "12345");

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(unica.Id, atual.EquipamentoId); // decisão explícita, mesmo sem ninguém escolher
    }

    [Fact]
    public async Task Autorizar_sem_equipamento_cadastrado_nao_trava_a_recepcao()
    {
        // Cadastro de equipamento é tarefa de administrador: travar aqui deixaria o paciente
        // parado no balcão. Autoriza, e o erro "Sem equipamento configurado" aparece no envio.
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var s = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var service = CriarService(db, SeedSolicitacao.CpfAleatorio(), pacienteId, telefoneVerificado: "21999990000");

        await service.AutorizarAsync(s.Id, "12345");

        var atual = await db.ExamesImagem.Include(x => x.Solicitacao).AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Null(atual.EquipamentoId);
        Assert.NotNull(atual.Solicitacao!.AutorizadoEm);
    }
}
