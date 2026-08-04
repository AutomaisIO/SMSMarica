using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMarica.Core.Associacoes;
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
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Associacoes;

/// <summary>
/// Conciliação PACS-driven (<see cref="ExameAssociacaoService.ConciliarStudyAsync"/>):
/// o study que CHEGOU no PACS casa com a solicitação pelas chaves duráveis
/// (AccessionNumber → PatientID=nº SMS → StudyUID de worklist), sem depender de
/// quando a solicitação foi criada. Integração com Postgres real (Testcontainers).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConciliacaoStudyTests(PostgresFixture fixture)
{
    private static readonly DateTime DataEstudoDicom = new(2026, 7, 7, 10, 30, 0, DateTimeKind.Unspecified);

    private ExameAssociacaoService CriarService(SmsMaricaDbContext db)
    {
        var consultaStudy = Substitute.For<IConsultaStudyClient>();
        consultaStudy.ObterDataHoraEstudoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DataEstudoDicom);

        // SolicitacoesExameService REAL: a promoção a Realizada (status/RealizadoEm/DataEstudo)
        // precisa acontecer de verdade; enfileiramento de comunicação fica no substitute.
        var solicitacoes = new SolicitacoesExameService(
            db,
            Substitute.For<IGeradorIdentificadores>(),
            Substitute.For<IDcm4cheeMwlClient>(),
            new ResolvedorEstacaoWorklist(db, NullLogger<ResolvedorEstacaoWorklist>.Instance),
            Substitute.For<INotificadorExame>(),
            new UsuarioAtualAccessorFake(),
            Substitute.For<IPacienteResolver>(),
            Substitute.For<IDispensaContatoService>(),
            new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
            new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
            Substitute.For<IRegistroErroService>(),
            Substitute.For<SMSMarica.Core.Auditoria.IAuditoriaService>(),
            NullLogger<SolicitacoesExameService>.Instance);

        return new ExameAssociacaoService(
            db,
            Substitute.For<IPacienteResolver>(),
            consultaStudy,
            solicitacoes,
            new UsuarioAtualAccessorFake(),
            NullLogger<ExameAssociacaoService>.Instance);
    }

    private static string UidAleatorio() =>
        $"1.2.392.{Random.Shared.NextInt64(100_000_000):D9}.{Random.Shared.NextInt64(100_000_000):D9}";

    [Fact]
    public async Task Worklist_uid_herdado_promove_a_realizada_sem_associacao_explicita()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);

        var resultado = await service.ConciliarStudyAsync(
            new EstudoPacsRecente(s.StudyInstanceUID, s.AccessionNumber, SeedSolicitacao.CpfAleatorio()));

        Assert.Equal(ResultadoConciliacao.Conciliada, resultado);
        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(StatusSolicitacaoExame.Realizada, atual.Status);
        Assert.NotNull(atual.RealizadoEm);
        Assert.Equal(DataEstudoDicom, atual.DataEstudo);
        // Worklist genuíno: vínculo é implícito (StudyUID) — NÃO cria associação explícita.
        Assert.False(await db.ExameAssociacoes.AsNoTracking()
            .AnyAsync(a => a.StudyInstanceUID == s.StudyInstanceUID));
    }

    [Fact]
    public async Task Accession_casa_mas_uid_proprio_da_maquina_cria_associacao_explicita()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);
        var uidDaMaquina = UidAleatorio(); // máquina ignorou o UID pré-gerado da worklist

        var resultado = await service.ConciliarStudyAsync(
            new EstudoPacsRecente(uidDaMaquina, s.AccessionNumber, SeedSolicitacao.CpfAleatorio()));

        Assert.Equal(ResultadoConciliacao.Conciliada, resultado);
        var assoc = await db.ExameAssociacoes.AsNoTracking()
            .SingleAsync(a => a.StudyInstanceUID == uidDaMaquina && a.ExcluidoEm == null);
        Assert.Equal(s.Id, assoc.ExameImagemId);
        Assert.Equal(OrigemAssociacaoExame.Automatica, assoc.Origem);
        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(StatusSolicitacaoExame.Realizada, atual.Status);
    }

    [Fact]
    public async Task PatientId_com_numero_do_pedido_associa_sem_accession()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);
        var uid = UidAleatorio();

        // Técnico digitou o nº da solicitação no campo Patient ID do equipamento.
        var resultado = await service.ConciliarStudyAsync(
            new EstudoPacsRecente(uid, AccessionNumber: null, PatientId: s.AccessionNumber));

        Assert.Equal(ResultadoConciliacao.Conciliada, resultado);
        Assert.True(await db.ExameAssociacoes.AsNoTracking()
            .AnyAsync(a => a.StudyInstanceUID == uid && a.ExameImagemId == s.Id && a.ExcluidoEm == null));
    }

    [Fact]
    public async Task Worklist_que_voltou_sem_accession_casa_pelo_uid_pregerado()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);

        var resultado = await service.ConciliarStudyAsync(
            new EstudoPacsRecente(s.StudyInstanceUID, AccessionNumber: null, PatientId: SeedSolicitacao.CpfAleatorio()));

        Assert.Equal(ResultadoConciliacao.Conciliada, resultado);
        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(StatusSolicitacaoExame.Realizada, atual.Status);
        Assert.False(await db.ExameAssociacoes.AsNoTracking()
            .AnyAsync(a => a.StudyInstanceUID == s.StudyInstanceUID));
    }

    [Fact]
    public async Task Study_sem_casamento_fica_orfao()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);

        var resultado = await service.ConciliarStudyAsync(
            new EstudoPacsRecente(UidAleatorio(), "ACC-INEXISTENTE", "07072026"));

        Assert.Equal(ResultadoConciliacao.SemSolicitacao, resultado);
        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(StatusSolicitacaoExame.Solicitada, atual.Status); // intocada
    }

    [Fact]
    public async Task Solicitacao_cancelada_nao_concilia()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        s.Status = StatusSolicitacaoExame.Cancelada;
        await db.SaveChangesAsync();
        var service = CriarService(db);

        var resultado = await service.ConciliarStudyAsync(
            new EstudoPacsRecente(s.StudyInstanceUID, s.AccessionNumber, null));

        Assert.Equal(ResultadoConciliacao.SemSolicitacao, resultado);
    }

    [Fact]
    public async Task Conciliar_worklist_duas_vezes_e_idempotente()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);
        var estudo = new EstudoPacsRecente(s.StudyInstanceUID, s.AccessionNumber, null);

        Assert.Equal(ResultadoConciliacao.Conciliada, await service.ConciliarStudyAsync(estudo));
        Assert.Equal(ResultadoConciliacao.JaConciliada, await service.ConciliarStudyAsync(estudo));
    }

    [Fact]
    public async Task Conciliar_associacao_explicita_duas_vezes_e_idempotente()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);
        var estudo = new EstudoPacsRecente(UidAleatorio(), s.AccessionNumber, null);

        Assert.Equal(ResultadoConciliacao.Conciliada, await service.ConciliarStudyAsync(estudo));
        Assert.Equal(ResultadoConciliacao.JaConciliada, await service.ConciliarStudyAsync(estudo));
    }

    [Fact]
    public async Task Tombstone_vinculo_desfeito_por_humano_nao_e_recriado_pelo_motor()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);
        var uid = UidAleatorio();
        var estudo = new EstudoPacsRecente(uid, s.AccessionNumber, null);

        // Motor associa; humano desfaz; motor NÃO pode recriar (as chaves DICOM continuam
        // no study) — o study vira órfão, gerido na tela de conferência.
        Assert.Equal(ResultadoConciliacao.Conciliada, await service.ConciliarStudyAsync(estudo));
        await service.DesassociarAsync(uid);
        Assert.Equal(ResultadoConciliacao.SemSolicitacao, await service.ConciliarStudyAsync(estudo));

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(StatusSolicitacaoExame.Solicitada, atual.Status); // reversão preservada
    }

    [Fact]
    public async Task Segunda_aquisicao_mesmo_accession_associa_a_solicitacao_realizada()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);

        // 1ª aquisição via worklist promove a Realizada.
        Assert.Equal(ResultadoConciliacao.Conciliada, await service.ConciliarStudyAsync(
            new EstudoPacsRecente(s.StudyInstanceUID, s.AccessionNumber, null)));

        // Técnico repete a aquisição: novo UID, mesmo accession → associa ao MESMO pedido.
        var uid2 = UidAleatorio();
        Assert.Equal(ResultadoConciliacao.Conciliada, await service.ConciliarStudyAsync(
            new EstudoPacsRecente(uid2, s.AccessionNumber, null)));
        Assert.True(await db.ExameAssociacoes.AsNoTracking()
            .AnyAsync(a => a.StudyInstanceUID == uid2 && a.ExameImagemId == s.Id && a.ExcluidoEm == null));
    }

    [Fact]
    public async Task Associar_de_novo_repara_promocao_perdida()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);
        var uid = UidAleatorio();

        // Simula falha parcial histórica: associação persistida SEM a promoção.
        db.ExameAssociacoes.Add(new SMSMarica.Data.Entities.ExameAssociacao
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = uid,
            ExameImagemId = s.Id,
            PacienteId = s.Solicitacao!.PacienteId,
            Origem = OrigemAssociacaoExame.Automatica,
            StatusSolicitacaoAnterior = StatusSolicitacaoExame.Solicitada,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Retry idempotente do POST deve AUTO-REPARAR (promover), não só devolver o DTO.
        await service.AssociarAsync(
            new Core.Associacoes.Dtos.AssociarExameRequest(uid, s.AccessionNumber), validarNoPacs: false);

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(x => x.Id == s.Id);
        Assert.Equal(StatusSolicitacaoExame.Realizada, atual.Status);
    }

    [Fact]
    public async Task Desassociar_remove_comunicacao_exame_liberado_pendente()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);
        var uid = UidAleatorio();

        Assert.Equal(ResultadoConciliacao.Conciliada, await service.ConciliarStudyAsync(
            new EstudoPacsRecente(uid, s.AccessionNumber, null)));

        // O zap "Exame Liberado" ficou na fila (Pendente) — aqui semeado direto porque o
        // serviço de comunicação é substituído no teste.
        db.ComunicacoesPaciente.Add(new SMSMarica.Data.Entities.ComunicacaoPaciente
        {
            Id = Guid.NewGuid(),
            Finalidade = FinalidadeComunicacao.ExameLiberado,
            SolicitacaoId = s.SolicitacaoId,
            PacienteId = s.Solicitacao!.PacienteId,
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await service.DesassociarAsync(uid);

        // Pendente removida: o exame não aconteceu, e a linha calaria a notificação real futura.
        Assert.False(await db.ComunicacoesPaciente.AsNoTracking().AnyAsync(
            c => c.SolicitacaoId == s.SolicitacaoId && c.Finalidade == FinalidadeComunicacao.ExameLiberado));
    }

    [Fact]
    public async Task Lote_conta_conciliadas_ja_conciliadas_e_orfaos()
    {
        await using var db = fixture.CriarDbContext();
        var s = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var service = CriarService(db);
        var lote = new[]
        {
            new EstudoPacsRecente(s.StudyInstanceUID, s.AccessionNumber, null), // worklist
            new EstudoPacsRecente(UidAleatorio(), "SEM-PEDIDO", null),          // órfão
        };

        var r1 = await service.ConciliarLoteAsync(lote);
        Assert.Equal((1, 0, 1, 0), (r1.Conciliadas, r1.JaConciliadas, r1.SemSolicitacao, r1.Falhas));

        // 2ª passada (o poller repassa a mesma janela): pré-filtro pega o já consumado.
        var r2 = await service.ConciliarLoteAsync(lote);
        Assert.Equal((0, 1, 1, 0), (r2.Conciliadas, r2.JaConciliadas, r2.SemSolicitacao, r2.Falhas));
    }
}
