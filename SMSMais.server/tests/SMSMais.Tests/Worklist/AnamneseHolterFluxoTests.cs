using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Anamneses;
using SMSMais.Core.Anamneses.Dtos;
using SMSMais.Core.Equipamentos;
using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Core.Erros;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Notificacoes;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Solicitacoes;
using SMSMais.Core.Solicitacoes.Identificadores;
using SMSMais.Core.Telefones;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Worklist;

/// <summary>
/// Anamnese do Holter, com banco: o exame espera a anamnese para ir à worklist do CardioNet, salvar
/// é o que o manda, corrigir regrava o item — e o questionário é do pedido, como a anamnese.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AnamneseHolterFluxoTests(PostgresFixture fixture)
{
    private const string Holter = """{"alturaCm":172,"pesoKg":80.5,"fumante":"parou"}""";

    [Fact]
    public async Task Holter_autorizado_sem_anamnese_espera_e_nao_vai_a_worklist()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SemearHolterAsync(db);

        await CriarSolicitacoes(db, exame.Solicitacao!.PacienteId).AutorizarAsync(exame.Id, "12345");

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == exame.Id);
        Assert.Null(atual.ProximaTentativaEm);
        Assert.Contains("Aguardando a anamnese", atual.ErroIntegracaoPacs);
    }

    [Fact]
    public async Task Salvar_a_anamnese_do_Holter_autorizado_manda_o_exame_a_worklist()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SemearHolterAsync(db);
        var pacienteId = exame.Solicitacao!.PacienteId;
        await CriarSolicitacoes(db, pacienteId).AutorizarAsync(exame.Id, "12345");
        db.ChangeTracker.Clear();

        var salvo = await CriarAnamneses(db, pacienteId).SalvarAsync(exame.Id, new SalvarAnamneseDto("holter", 1, Holter, null));

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == exame.Id);
        Assert.NotNull(atual.ProximaTentativaEm);
        Assert.Null(atual.ErroIntegracaoPacs);
        Assert.Contains("indo para a worklist", salvo.AvisoWorklist);
    }

    [Fact]
    public async Task Worker_leva_altura_peso_e_fumante_ao_item()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SemearHolterAsync(db);
        var pacienteId = exame.Solicitacao!.PacienteId;
        db.Anamneses.Add(new Anamnese
        {
            Id = Guid.NewGuid(), ExameImagemId = exame.Id, Tipo = AnamneseHolter.Tipo, Versao = 1,
            ConteudoJson = Holter, CriadoEm = DateTime.UtcNow,
        });
        var s = await db.ExamesImagem.SingleAsync(e => e.Id == exame.Id);
        s.ProximaTentativaEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var mwl = Substitute.For<IDcm4cheeMwlClient>();
        mwl.CriarOuAtualizarMwlItemAsync(Arg.Any<ExameImagem>(), Arg.Any<DadosClinicosWorklist?>(), Arg.Any<CancellationToken>())
            .Returns("SPS-1");

        await CriarSolicitacoes(db, pacienteId, mwl).ProcessarTentativaEnvioAsync(exame.Id);

        await mwl.Received(1).CriarOuAtualizarMwlItemAsync(
            Arg.Any<ExameImagem>(),
            Arg.Is<DadosClinicosWorklist?>(c => c != null && c.AlturaMetros == 1.72m && c.PesoKg == 80.5m && c.SmokingStatus == "NO"),
            Arg.Any<CancellationToken>());
        Assert.Equal(StatusSolicitacaoExame.Enviada,
            (await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == exame.Id)).Status);
    }

    [Fact]
    public async Task Worker_nao_envia_Holter_sem_anamnese()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SemearHolterAsync(db);
        var s = await db.ExamesImagem.SingleAsync(e => e.Id == exame.Id);
        s.ProximaTentativaEm = DateTime.UtcNow;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var mwl = Substitute.For<IDcm4cheeMwlClient>();

        await CriarSolicitacoes(db, exame.Solicitacao!.PacienteId, mwl).ProcessarTentativaEnvioAsync(exame.Id);

        await mwl.DidNotReceiveWithAnyArgs().CriarOuAtualizarMwlItemAsync(default!, default(DadosClinicosWorklist?), default);
        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == exame.Id);
        Assert.Equal(StatusSolicitacaoExame.Solicitada, atual.Status);
        Assert.Null(atual.ProximaTentativaEm);
        Assert.Contains("Aguardando a anamnese", atual.ErroIntegracaoPacs);
    }

    [Fact]
    public async Task Corrigir_a_anamnese_de_um_Holter_ja_na_worklist_regrava_o_item()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SemearHolterAsync(db);
        var pacienteId = exame.Solicitacao!.PacienteId;
        var s = await db.ExamesImagem.Include(e => e.Solicitacao).SingleAsync(e => e.Id == exame.Id);
        s.Solicitacao!.AutorizadoEm = DateTime.UtcNow;
        s.Status = StatusSolicitacaoExame.Recebida;
        s.WorklistItemUid = "SPS-1";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var salvo = await CriarAnamneses(db, pacienteId).SalvarAsync(exame.Id, new SalvarAnamneseDto("holter", 1, Holter, null));

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == exame.Id);
        Assert.Equal(StatusSolicitacaoExame.Solicitada, atual.Status);
        Assert.NotNull(atual.ProximaTentativaEm);
        Assert.Contains("corrigida", salvo.AvisoWorklist);
    }

    [Fact]
    public async Task Aparelho_com_a_anamnese_desligada_nao_segura_o_Holter()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SemearHolterAsync(db);
        await new EquipamentosService(db, new UsuarioAtualAccessorFake()).CadastrarAsync(new CadastrarEquipamentoRequest(
            "CARDIO TESTE", exame.Solicitacao!.UnidadeExecutanteId, ModalidadeDicom.ECG, null, UsaAnamnese: false));
        db.ChangeTracker.Clear();

        await CriarSolicitacoes(db, exame.Solicitacao!.PacienteId).AutorizarAsync(exame.Id, "12345");

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == exame.Id);
        Assert.DoesNotContain("Aguardando a anamnese", atual.ErroIntegracaoPacs ?? string.Empty);
    }

    /// <summary>
    /// Pedido de MAPA (principal) + Holter (extra): a tela abre pelo Holter e a anamnese mora no
    /// principal. Os dois lados têm de concordar que o questionário é o do Holter.
    /// </summary>
    [Fact]
    public async Task Pedido_com_Holter_no_extra_abre_e_salva_o_questionario_do_Holter()
    {
        await using var db = fixture.CriarDbContext();
        var principal = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var tipoPrincipal = await db.TiposExame.SingleAsync(t => t.Id == principal.TipoExameId);
        tipoPrincipal.ModalidadeDicom = ModalidadeDicom.OT;
        var ecg = new TipoExame
        {
            Id = Guid.NewGuid(), Nome = $"HOLTER TESTE {Guid.NewGuid():N}"[..24],
            ProcedimentoSigtapId = tipoPrincipal.ProcedimentoSigtapId, ModalidadeDicom = ModalidadeDicom.ECG,
            CriadoEm = DateTime.UtcNow,
        };
        var extra = new ExameImagem
        {
            Id = Guid.NewGuid(), SolicitacaoId = principal.SolicitacaoId, Ordem = 1,
            AccessionNumber = $"{principal.AccessionNumber}-1",
            StudyInstanceUID = $"{principal.StudyInstanceUID}.1",
            TipoExameId = ecg.Id, Status = StatusSolicitacaoExame.Solicitada, CriadoEm = DateTime.UtcNow,
        };
        db.TiposExame.Add(ecg);
        db.ExamesImagem.Add(extra);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var anamneses = CriarAnamneses(db, principal.Solicitacao!.PacienteId);

        Assert.Equal("holter", (await anamneses.ObterContextoAsync(extra.Id, null)).Questionario);
        Assert.Equal("holter", (await anamneses.ObterContextoAsync(principal.Id, null)).Questionario);
        await anamneses.SalvarAsync(extra.Id, new SalvarAnamneseDto("holter", 1, Holter, null));

        Assert.True(await db.Anamneses.AnyAsync(a => a.ExameImagemId == principal.Id && a.Tipo == AnamneseHolter.Tipo));
    }

    [Fact]
    public async Task Fora_do_Holter_o_questionario_do_Holter_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var anamneses = CriarAnamneses(db, exame.Solicitacao!.PacienteId);

        Assert.Equal("mamografia", (await anamneses.ObterContextoAsync(exame.Id, null)).Questionario);
        await Assert.ThrowsAsync<SMSMais.Core.Common.Excecoes.ValidacaoException>(() =>
            anamneses.SalvarAsync(exame.Id, new SalvarAnamneseDto("holter", 1, Holter, null)));
    }

    /// <summary>Exame cujo tipo é ECG (o Holter), sem aparelho na unidade — a regra cai no lado
    /// seguro, com anamnese.</summary>
    private static async Task<ExameImagem> SemearHolterAsync(SmsMaisDbContext db)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var tipo = await db.TiposExame.SingleAsync(t => t.Id == exame.TipoExameId);
        tipo.ModalidadeDicom = ModalidadeDicom.ECG;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return exame;
    }

    private static IPacienteResolver Resolver(Guid pacienteId)
    {
        var resolver = Substitute.For<IPacienteResolver>();
        resolver.ResolverAsync(pacienteId, Arg.Any<CancellationToken>())
            .Returns(new PacienteResumo(
                pacienteId, "PACIENTE TESTE", SeedSolicitacao.CpfAleatorio(), null, null,
                Sexo.NaoInformado, SeedSolicitacao.TelefoneAleatorio()));
        return resolver;
    }

    private static AnamnesesService CriarAnamneses(SmsMaisDbContext db, Guid pacienteId) => new(
        db, Resolver(pacienteId),
        Substitute.For<SMSMais.Core.Common.Cid.ICidCatalogoService>(),
        new UsuarioAtualAccessorFake(Guid.NewGuid()),
        CriarSolicitacoes(db, pacienteId));

    private static SolicitacoesService CriarSolicitacoes(
        SmsMaisDbContext db, Guid pacienteId, IDcm4cheeMwlClient? mwl = null) => new(
        db,
        Substitute.For<IGeradorIdentificadores>(),
        mwl ?? Substitute.For<IDcm4cheeMwlClient>(),
        new ResolvedorEstacaoWorklist(db, new EscopoExameUnidade(db), NullLogger<ResolvedorEstacaoWorklist>.Instance),
        new EscopoExameUnidade(db),
        Substitute.For<INotificadorExame>(),
        new UsuarioAtualAccessorFake(Guid.NewGuid()),
        Resolver(pacienteId),
        Substitute.For<SMSMais.Core.Pacientes.IPacientesService>(),
        new DispensaContatoService(db, Substitute.For<IPacienteFhirClient>(),
            new UsuarioAtualAccessorFake(Guid.NewGuid()), NullLogger<DispensaContatoService>.Instance),
        new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
        new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
        Substitute.For<IRegistroErroService>(),
        Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>(),
        Substitute.For<SMSMais.Core.Common.Cid.ICidCatalogoService>(),
        NullLogger<SolicitacoesService>.Instance);
}
