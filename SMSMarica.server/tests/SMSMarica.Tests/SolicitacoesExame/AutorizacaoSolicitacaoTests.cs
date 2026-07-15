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
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
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
            Substitute.For<INotificadorExame>(),
            new UsuarioAtualAccessorFake(Guid.NewGuid()),
            resolver,
            new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
            new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
            NullLogger<SolicitacoesExameService>.Instance);
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
}
