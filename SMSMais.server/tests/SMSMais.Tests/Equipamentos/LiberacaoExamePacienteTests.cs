using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SMSMais.Core.Anexos;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Equipamentos;
using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Core.Equipamentos.Validators;
using SMSMais.Core.Erros;
using SMSMais.Core.Exames;
using SMSMais.Core.Integracoes.SisregWeb.Chave;
using SMSMais.Core.Laudos;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Laudos.Dtos;
using SMSMais.Core.Notificacoes;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Solicitacoes;
using SMSMais.Core.Solicitacoes.Identificadores;
using SMSMais.Core.Telefones;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Equipamentos;

/// <summary>
/// Liberação do exame ao paciente pela regra do aparelho (ADR-0075): o ultrassom (laudado fora)
/// libera ao chegar; a mamografia e a densitometria (laudadas aqui) seguram a imagem até o laudo
/// assinado, e o paciente recebe as duas coisas juntas.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class LiberacaoExamePacienteTests(PostgresFixture fixture)
{
    // ===================== a regra, sem banco =====================

    private static RegraDoExame Regra(string nome, OrigemLaudo laudo, LiberacaoExamePaciente liberacao, bool anamnese = true) =>
        new(Guid.NewGuid(), nome, laudo, liberacao, anamnese);

    /// <summary>Os dois ultrassons do CDT: nenhum escolhido, mas concordam — vale a regra deles.</summary>
    [Fact]
    public void Candidatos_que_concordam_dao_a_regra_deles()
    {
        var regra = RegraDoExame.Combinar([
            Regra("Ultrassom 01", OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar),
            Regra("Ultrassom 02", OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar),
        ]);

        Assert.True(regra.LiberaAoChegar);
        Assert.False(regra.LaudoNaPlataforma);
        Assert.Equal("Ultrassom 01 ou Ultrassom 02", regra.EquipamentoNome);
    }

    /// <summary>
    /// Discordando, cada campo cai para o lado que não erra: soltar a imagem antes da hora não tem
    /// volta (retém), e esconder o "Laudar" ou a anamnese de quem precisa custa mais do que mostrar.
    /// </summary>
    [Fact]
    public void Candidatos_que_discordam_retem_e_mantem_laudo_e_anamnese()
    {
        var regra = RegraDoExame.Combinar([
            Regra("Mamógrafo", OrigemLaudo.Plataforma, LiberacaoExamePaciente.ComLaudoAssinado, anamnese: false),
            Regra("Ultrassom", OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar, anamnese: true),
        ]);

        Assert.Equal(LiberacaoExamePaciente.ComLaudoAssinado, regra.Liberacao);
        Assert.True(regra.LaudoNaPlataforma);
        Assert.True(regra.UsaAnamnese);
    }

    [Fact]
    public void Sem_candidato_fica_no_lado_seguro()
    {
        var regra = RegraDoExame.Combinar([]);

        Assert.Same(RegraDoExame.SemAparelho, regra);
        Assert.False(regra.LiberaAoChegar);
        Assert.True(regra.LaudoNaPlataforma);
    }

    /// <summary>"Só com o laudo" com o laudo feito fora prenderia o exame para sempre.</summary>
    [Theory]
    [InlineData(OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.ComLaudoAssinado, false)]
    [InlineData(OrigemLaudo.JuntoComExame, LiberacaoExamePaciente.ComLaudoAssinado, false)]
    [InlineData(OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar, true)]
    [InlineData(OrigemLaudo.Plataforma, LiberacaoExamePaciente.ComLaudoAssinado, true)]
    [InlineData(OrigemLaudo.Plataforma, LiberacaoExamePaciente.AoChegar, true)]
    public void Cadastro_recusa_reter_ate_um_laudo_que_nao_existe(
        OrigemLaudo laudo, LiberacaoExamePaciente liberacao, bool valido)
    {
        var request = new CadastrarEquipamentoRequest(
            "Aparelho", Guid.NewGuid(), ModalidadeDicom.US, null, Laudo: laudo, LiberacaoPaciente: liberacao);

        Assert.Equal(valido, new CadastrarEquipamentoValidator().Validate(request).IsValid);
    }

    /// <summary>
    /// Cadastro sem os campos (script, outra tela) recebe o padrão da modalidade: um ultrassom que
    /// nascesse "só com o laudo" reteria exame para sempre. E o "sem anamnese" tem de chegar ao banco —
    /// com default no banco, o EF omitiria o false do INSERT.
    /// </summary>
    [Fact]
    public async Task Cadastro_sem_os_campos_recebe_o_padrao_da_modalidade_e_grava_sem_anamnese()
    {
        await using var db = fixture.CriarDbContext();
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNIDADE {Guid.NewGuid():N}"[..20], CriadoEm = DateTime.UtcNow };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        var servico = new EquipamentosService(db, new UsuarioAtualAccessorFake());

        var us = await servico.CadastrarAsync(new CadastrarEquipamentoRequest("US", unidade.Id, ModalidadeDicom.US, null));
        var mg = await servico.CadastrarAsync(new CadastrarEquipamentoRequest("MG", unidade.Id, ModalidadeDicom.MG, null));
        var mapa = await servico.CadastrarAsync(new CadastrarEquipamentoRequest(
            "MAPA", unidade.Id, ModalidadeDicom.OT, null, Laudo: OrigemLaudo.JuntoComExame, UsaAnamnese: false));

        var gravados = await db.Equipamentos.AsNoTracking()
            .Where(e => e.Id == us || e.Id == mg || e.Id == mapa).ToDictionaryAsync(e => e.Id);
        Assert.Equal((OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar),
            (gravados[us].Laudo, gravados[us].LiberacaoPaciente));
        Assert.Equal((OrigemLaudo.Plataforma, LiberacaoExamePaciente.ComLaudoAssinado),
            (gravados[mg].Laudo, gravados[mg].LiberacaoPaciente));
        Assert.Equal(LiberacaoExamePaciente.AoChegar, gravados[mapa].LiberacaoPaciente);
        Assert.False(gravados[mapa].UsaAnamnese);
        Assert.True(gravados[mg].UsaAnamnese);
    }

    // ===================== chegada do estudo =====================

    [Fact]
    public async Task Aparelho_que_libera_ao_chegar_libera_e_avisa()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        await AparelhoAsync(db, exame, OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar, doExame: true);
        var comunicacoes = Substitute.For<IComunicacaoPacienteService>();

        await Solicitacoes(db, comunicacoes).MarcarComoRealizadaAsync(exame.Id, DateTime.UtcNow, null);

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == exame.Id);
        Assert.NotNull(atual.LiberadoPacienteEm);
        Assert.Null(atual.LiberadoPacientePor);
        await comunicacoes.Received(1).EnfileirarAsync(
            Arg.Any<Solicitacao>(), FinalidadeComunicacao.ExameLiberado, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Aparelho_que_so_libera_com_laudo_retem_e_nao_avisa()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        await AparelhoAsync(db, exame, OrigemLaudo.Plataforma, LiberacaoExamePaciente.ComLaudoAssinado, doExame: true);
        var comunicacoes = Substitute.For<IComunicacaoPacienteService>();

        await Solicitacoes(db, comunicacoes).MarcarComoRealizadaAsync(exame.Id, DateTime.UtcNow, null);

        var atual = await db.ExamesImagem.AsNoTracking().SingleAsync(e => e.Id == exame.Id);
        Assert.Equal(StatusSolicitacaoExame.Realizada, atual.Status);
        Assert.Null(atual.LiberadoPacienteEm);
        await comunicacoes.DidNotReceive().EnfileirarAsync(
            Arg.Any<Solicitacao>(), FinalidadeComunicacao.ExameLiberado, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Exame sem aparelho gravado, numa unidade com dois aparelhos da modalidade que liberam ao
    /// chegar: é o caso dos 78 ultrassons de 20/10/2026 — não pode ficar retido por indecisão.
    /// </summary>
    [Fact]
    public async Task Sem_aparelho_gravado_vale_a_regra_comum_dos_candidatos()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        await AparelhoAsync(db, exame, OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar, doExame: false);
        await AparelhoAsync(db, exame, OrigemLaudo.ForaDaPlataforma, LiberacaoExamePaciente.AoChegar, doExame: false);

        var regra = await new RegrasDoExameService(db).ResolverAsync(exame.Id);

        Assert.True(regra.LiberaAoChegar);
        Assert.Null(regra.EquipamentoId);
        Assert.Contains(" ou ", regra.EquipamentoNome);
    }

    // ===================== o que o paciente vê =====================

    [Fact]
    public async Task App_do_cidadao_esconde_o_retido_e_a_equipe_ve_tudo()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var liberado = await RealizadoAsync(db, pacienteId, liberadoEm: DateTime.UtcNow);
        var retido = await RealizadoAsync(db, pacienteId, liberadoEm: null);
        var clinico = Clinico(db);

        var doPaciente = await clinico.ListarExamesAsync(pacienteId);
        var daEquipe = await clinico.ListarExamesAsync(pacienteId, visaoDaEquipe: true);

        Assert.Equal([liberado.Id], doPaciente.Select(e => e.Id));
        Assert.Equal(2, daEquipe.Count);
        Assert.Null(await clinico.ObterImagensPdfAsync(pacienteId, retido.Id));
    }

    /// <summary>O laudo assinado libera mesmo se a liberação não ficou gravada (a aprovação é
    /// best-effort): é a mesma regra, lida pelo outro lado.</summary>
    [Fact]
    public async Task Laudo_assinado_libera_o_exame_retido()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var retido = await RealizadoAsync(db, pacienteId, liberadoEm: null);
        var laudoId = Guid.NewGuid();
        var laudos = Substitute.For<ILaudosService>();
        laudos.ListarPorStudyUidsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([new LaudoPorStudyDto(retido.StudyInstanceUID, laudoId, 1, StatusLaudo.Finalizado, true)]);
        var assinatura = Substitute.For<ILaudoAssinaturaService>();
        assinatura.QuaisAssinadosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { laudoId });

        var exames = await Clinico(db, laudos, assinatura).ListarExamesAsync(pacienteId);

        var exame = Assert.Single(exames);
        Assert.Equal(retido.Id, exame.Id);
        Assert.True(exame.LaudoAssinado);
    }

    // ===================== a fila do WhatsApp =====================

    /// <summary>
    /// O "Exame liberado" que já estava na fila antes da regra (ou que voltou da espera do telefone
    /// verificado) não sai para exame retido: a imagem vai junto com o laudo, no "Laudo pronto".
    /// </summary>
    [Fact]
    public async Task Aviso_de_exame_retido_sai_da_fila_como_dispensado()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await RealizadoAsync(db, Guid.NewGuid(), liberadoEm: null);
        var aviso = await AvisoExameLiberadoAsync(db, exame);
        var whats = Substitute.For<IWhatsAppCliente>();

        await Comunicacoes(db, whats).ProcessarTentativaEnvioAsync(aviso.Id);

        var atual = await db.ComunicacoesPaciente.AsNoTracking().SingleAsync(c => c.Id == aviso.Id);
        Assert.Equal(StatusComunicacao.Dispensada, atual.Status);
        Assert.StartsWith("Retido até o laudo", atual.MotivoFalha);
        Assert.Empty(whats.ReceivedCalls());
    }

    [Fact]
    public async Task Reenviar_o_aviso_de_exame_retido_explica_como_liberar()
    {
        await using var db = fixture.CriarDbContext();
        var exame = await RealizadoAsync(db, Guid.NewGuid(), liberadoEm: null);
        var aviso = await AvisoExameLiberadoAsync(db, exame);

        var erro = await Assert.ThrowsAsync<SMSMais.Core.Common.Excecoes.ConflitoException>(
            () => Comunicacoes(db, Substitute.For<IWhatsAppCliente>()).ReenviarAsync(exame.Id, aviso.Id));

        Assert.Contains("Enviar exame", erro.Message);
    }

    // ===================== apoio =====================

    private static async Task<Equipamento> AparelhoAsync(
        SmsMaisDbContext db, ExameImagem exame, OrigemLaudo laudo, LiberacaoExamePaciente liberacao, bool doExame)
    {
        var unidadeId = (await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == exame.SolicitacaoId))
            .UnidadeExecutanteId;
        var aparelho = new Equipamento
        {
            Id = Guid.CreateVersion7(),
            Nome = $"Aparelho {Guid.NewGuid():N}"[..20],
            UnidadeId = unidadeId,
            // O tipo do seed é mamografia: a dedução por modalidade procura MG na unidade.
            ModalidadeDicom = ModalidadeDicom.MG,
            Laudo = laudo,
            LiberacaoPaciente = liberacao,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Equipamentos.Add(aparelho);
        if (doExame)
        {
            var e = await db.ExamesImagem.SingleAsync(x => x.Id == exame.Id);
            e.EquipamentoId = aparelho.Id;
        }
        await db.SaveChangesAsync();
        return aparelho;
    }

    private static async Task<ExameImagem> RealizadoAsync(SmsMaisDbContext db, Guid pacienteId, DateTime? liberadoEm)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var e = await db.ExamesImagem.Include(x => x.Solicitacao).SingleAsync(x => x.Id == exame.Id);
        e.Status = StatusSolicitacaoExame.Realizada;
        e.RealizadoEm = DateTime.UtcNow;
        e.LiberadoPacienteEm = liberadoEm;
        e.Solicitacao!.Status = StatusSolicitacao.Realizada;
        await db.SaveChangesAsync();
        return e;
    }

    private static async Task<ComunicacaoPaciente> AvisoExameLiberadoAsync(SmsMaisDbContext db, ExameImagem exame)
    {
        var aviso = new ComunicacaoPaciente
        {
            Id = Guid.CreateVersion7(),
            Tipo = TipoAgendamento.Exame,
            Finalidade = FinalidadeComunicacao.ExameLiberado,
            SolicitacaoId = exame.SolicitacaoId,
            PacienteId = exame.Solicitacao!.PacienteId,
            Status = StatusComunicacao.Pendente,
            ProximaTentativaEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.ComunicacoesPaciente.Add(aviso);
        await db.SaveChangesAsync();
        return aviso;
    }

    private static SolicitacoesService Solicitacoes(SmsMaisDbContext db, IComunicacaoPacienteService comunicacoes) =>
        new(
            db,
            Substitute.For<IGeradorIdentificadores>(),
            Substitute.For<IDcm4cheeMwlClient>(),
            new ResolvedorEstacaoWorklist(db, new EscopoExameUnidade(db), NullLogger<ResolvedorEstacaoWorklist>.Instance),
            new EscopoExameUnidade(db),
            Substitute.For<INotificadorExame>(),
            new UsuarioAtualAccessorFake(),
            Substitute.For<IPacienteResolver>(),
            Substitute.For<IPacientesService>(),
            Substitute.For<IDispensaContatoService>(),
            new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
            new Lazy<IComunicacaoPacienteService>(() => comunicacoes),
            Substitute.For<IRegistroErroService>(),
            Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>(),
            Substitute.For<SMSMais.Core.Common.Cid.ICidCatalogoService>(),
            NullLogger<SolicitacoesService>.Instance);

    private static CidadaoClinicoService Clinico(
        SmsMaisDbContext db, ILaudosService? laudos = null, ILaudoAssinaturaService? assinatura = null)
    {
        if (laudos is null)
        {
            laudos = Substitute.For<ILaudosService>();
            laudos.ListarPorStudyUidsAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<LaudoPorStudyDto>());
        }
        if (assinatura is null)
        {
            assinatura = Substitute.For<ILaudoAssinaturaService>();
            assinatura.QuaisAssinadosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new HashSet<Guid>());
        }
        return new CidadaoClinicoService(
            db, Substitute.For<IAnexosService>(), laudos, assinatura, Substitute.For<IExameImagensPdfService>(),
            Substitute.For<IChaveConfirmacaoSisregService>(), Substitute.For<IAgendamentosPacienteService>(),
            Substitute.For<IPacientesService>(), Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>(),
            NullLogger<CidadaoClinicoService>.Instance);
    }

    private static ComunicacaoPacienteService Comunicacoes(SmsMaisDbContext db, IWhatsAppCliente whats) =>
        new(
            db, Substitute.For<IPacientesService>(), Substitute.For<ICidadaoLoginLinkService>(), whats,
            Options.Create(new ComunicacaoPacienteOptions()), new UsuarioAtualAccessorFake(),
            Substitute.For<IDispensaContatoService>(), Substitute.For<IContatoComprometidoService>(),
            Substitute.For<IConfirmacaoConfiguracaoService>(), NullLogger<ComunicacaoPacienteService>.Instance);
}
