using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Erros;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Notificacoes;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Solicitacoes;
using SMSMais.Core.Solicitacoes.Dtos;
using SMSMais.Core.Solicitacoes.Identificadores;
using SMSMais.Core.Telefones;
using SMSMais.Core.Worklist;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Solicitacoes;

/// <summary>
/// Exames e consultas numa lista só: a lista parte da espinha, o id público é o do exame quando
/// há satélite e o da espinha quando não há, e as ações de recepção (autorizar a chegada,
/// cancelar) valem para a consulta sem as travas que existem por causa do PACS.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ListaUnificadaTests(PostgresFixture fixture)
{
    private static SolicitacoesService CriarService(SmsMaisDbContext db, Guid pacienteId, string? cpf = null)
    {
        var resolver = Substitute.For<IPacienteResolver>();
        var resumo = new PacienteResumo(pacienteId, "PACIENTE TESTE", cpf, null, null, Sexo.NaoInformado, null);
        resolver.ResolverAsync(pacienteId, Arg.Any<CancellationToken>()).Returns(resumo);
        resolver.ResolverManyAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, PacienteResumo> { [pacienteId] = resumo });

        return new SolicitacoesService(
            db,
            Substitute.For<IGeradorIdentificadores>(),
            Substitute.For<IDcm4cheeMwlClient>(),
            new ResolvedorEstacaoWorklist(db, new EscopoExameUnidade(db), NullLogger<ResolvedorEstacaoWorklist>.Instance),
            new EscopoExameUnidade(db),
            Substitute.For<INotificadorExame>(),
            // Sem usuário no contexto: o escopo de unidade não restringe — os testes isolam pelo paciente.
            new UsuarioAtualAccessorFake(),
            resolver,
            Substitute.For<SMSMais.Core.Pacientes.IPacientesService>(),
            Substitute.For<IDispensaContatoService>(),
            new Lazy<ILaudoAssinaturaService>(() => Substitute.For<ILaudoAssinaturaService>()),
            new Lazy<IComunicacaoPacienteService>(() => Substitute.For<IComunicacaoPacienteService>()),
            Substitute.For<IRegistroErroService>(),
            Substitute.For<SMSMais.Core.Auditoria.IAuditoriaService>(),
            Substitute.For<SMSMais.Core.Common.Cid.ICidCatalogoService>(),
            NullLogger<SolicitacoesService>.Instance);
    }

    /// <summary>Consulta importada do SISREG: só a espinha, sem exame de imagem.</summary>
    private static async Task<Solicitacao> CriarConsultaAsync(
        SmsMaisDbContext db, Guid pacienteId, StatusSolicitacao status = StatusSolicitacao.Agendada)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"POLICLINICA TESTE {Guid.NewGuid():N}"[..30],
            CriadoEm = DateTime.UtcNow,
        };
        var consulta = new Solicitacao
        {
            Id = Guid.NewGuid(),
            PacienteId = pacienteId,
            Categoria = CategoriaSolicitacao.Consulta,
            EspecialidadeTexto = "CONSULTA EM CARDIOLOGIA",
            ProcedimentoTexto = "CONSULTA MEDICA EM ATENCAO ESPECIALIZADA",
            UnidadeExecutanteId = unidade.Id,
            SolicitanteNome = "DR TESTE",
            Status = status,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = DateTime.UtcNow.AddDays(2),
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        db.Solicitacoes.Add(consulta);
        await db.SaveChangesAsync();
        return consulta;
    }

    [Fact]
    public async Task Lista_traz_exame_e_consulta_do_paciente_juntos()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var exame = await SeedSolicitacao.CriarAsync(db, pacienteId);
        var consulta = await CriarConsultaAsync(db, pacienteId);

        var pagina = await CriarService(db, pacienteId).ListarAsync(new FiltroSolicitacoesDto(PacienteId: pacienteId));

        Assert.Equal(2, pagina.Total);
        var linhaExame = Assert.Single(pagina.Itens, i => i.Categoria == CategoriaSolicitacao.Imagem);
        Assert.Equal(exame.Id, linhaExame.Id);
        Assert.Equal(exame.SolicitacaoId, linhaExame.SolicitacaoId);

        var linhaConsulta = Assert.Single(pagina.Itens, i => i.Categoria == CategoriaSolicitacao.Consulta);
        Assert.Equal(consulta.Id, linhaConsulta.Id);
        Assert.Equal(consulta.Id, linhaConsulta.SolicitacaoId);
        Assert.Equal(StatusSolicitacaoExame.Agendada, linhaConsulta.Status);
        Assert.Equal("CONSULTA EM CARDIOLOGIA", linhaConsulta.EspecialidadeTexto);
        Assert.Equal("PACIENTE TESTE", linhaConsulta.PacienteNome);
        Assert.Equal(string.Empty, linhaConsulta.AccessionNumber);
    }

    [Fact]
    public async Task Filtro_por_tipo_separa_consulta_de_exame()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        await SeedSolicitacao.CriarAsync(db, pacienteId);
        var consulta = await CriarConsultaAsync(db, pacienteId);
        var service = CriarService(db, pacienteId);

        var consultas = await service.ListarAsync(new FiltroSolicitacoesDto(
            PacienteId: pacienteId, Categoria: CategoriaSolicitacao.Consulta));
        var exames = await service.ListarAsync(new FiltroSolicitacoesDto(
            PacienteId: pacienteId, Categoria: CategoriaSolicitacao.Imagem));

        Assert.Equal(consulta.Id, Assert.Single(consultas.Itens).Id);
        Assert.Equal(CategoriaSolicitacao.Imagem, Assert.Single(exames.Itens).Categoria);
    }

    [Fact]
    public async Task Filtro_de_status_usa_a_regua_comum_e_estado_so_de_execucao_nao_traz_consulta()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        await SeedSolicitacao.CriarAsync(db, pacienteId); // exame Solicitada
        var consulta = await CriarConsultaAsync(db, pacienteId); // consulta Agendada
        var service = CriarService(db, pacienteId);

        var agendadas = await service.ListarAsync(new FiltroSolicitacoesDto(
            Status: StatusSolicitacaoExame.Agendada, PacienteId: pacienteId));
        var enviadas = await service.ListarAsync(new FiltroSolicitacoesDto(
            Status: StatusSolicitacaoExame.Enviada, PacienteId: pacienteId));

        Assert.Equal(consulta.Id, Assert.Single(agendadas.Itens).Id);
        Assert.Empty(enviadas.Itens);
    }

    [Fact]
    public async Task Detalhe_da_consulta_abre_pelo_id_da_espinha()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var consulta = await CriarConsultaAsync(db, pacienteId);

        var dto = await CriarService(db, pacienteId).ObterPorIdAsync(consulta.Id);

        Assert.Equal(consulta.Id, dto.Id);
        Assert.Equal(CategoriaSolicitacao.Consulta, dto.Categoria);
        Assert.Equal("CONSULTA EM CARDIOLOGIA", dto.EspecialidadeTexto);
        Assert.Equal(StatusSolicitacaoExame.Agendada, dto.Status);
    }

    [Fact]
    public async Task Pedido_de_imagem_nao_abre_pelo_id_da_espinha()
    {
        // O id público do exame de imagem é o do satélite; a espinha dele não é outra porta.
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var exame = await SeedSolicitacao.CriarAsync(db, pacienteId);

        await Assert.ThrowsAsync<NaoEncontradoException>(
            () => CriarService(db, pacienteId).ObterPorIdAsync(exame.SolicitacaoId));
    }

    [Fact]
    public async Task Autorizar_consulta_registra_a_chegada_sem_as_travas_do_exame()
    {
        // Sem CPF e sem telefone verificado: as duas travas existem por causa do PACS e do laudo.
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var consulta = await CriarConsultaAsync(db, pacienteId);

        await CriarService(db, pacienteId, cpf: null).AutorizarAsync(consulta.Id, "12345");

        var atual = await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == consulta.Id);
        Assert.NotNull(atual.AutorizadoEm);
        Assert.Equal("12345", atual.ChaveConfirmacao);
        Assert.Equal(StatusConfirmacaoAgendamento.Confirmada, atual.StatusConfirmacao);
        Assert.Equal("presencial", atual.ConfirmadoCanal);
    }

    [Fact]
    public async Task Cancelar_consulta_agendada_cancela_a_espinha()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var consulta = await CriarConsultaAsync(db, pacienteId);

        await CriarService(db, pacienteId).CancelarAsync(consulta.Id, new CancelarSolicitacaoRequest("Paciente desistiu"));

        var atual = await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == consulta.Id);
        Assert.Equal(StatusSolicitacao.Cancelada, atual.Status);
        Assert.Equal("Paciente desistiu", atual.MotivoCancelamento);
        Assert.NotNull(atual.CanceladoEm);
    }

    [Fact]
    public async Task Cancelar_consulta_realizada_recusa()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var consulta = await CriarConsultaAsync(db, pacienteId, StatusSolicitacao.Realizada);

        await Assert.ThrowsAsync<ConflitoException>(() => CriarService(db, pacienteId)
            .CancelarAsync(consulta.Id, new CancelarSolicitacaoRequest("tarde demais")));
    }
}
