using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Regulacao.Catalogo.Dtos;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Solicitacoes;

/// <summary>
/// Abertura da solicitação e a passagem para a fila (planos 02 e 04).
///
/// <para>O que os testes prendem: o NAR não existe sem a unidade "em nome de" (sem ela o pedido
/// não tem como ser incluído no SISREG depois, D-9); a solicitação não sai do rascunho com CPF
/// faltando nem com campo obrigatório vazio — porque falhar aqui, com nome, é infinitamente
/// melhor do que falhar no envio ao sistema de terceiro; e o escopo por unidade é
/// <b>fail-closed</b>.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoSolicitacaoServiceTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private sealed class StoreFake : IArquivoExigenciaStore
    {
        public string MontarChave(Guid p, Guid a, string e) => $"Regulacao/{p:D}/{a:D}.{e}";
        public Task SalvarAsync(string c, byte[] b, CancellationToken ct) => Task.CompletedTask;
        public Task<byte[]?> LerAsync(string c, CancellationToken ct) => Task.FromResult<byte[]?>(null);
        public Task ExcluirAsync(string c, CancellationToken ct) => Task.CompletedTask;
    }

    private static (RegulacaoSolicitacaoService Servico, IPacientesService Pacientes,
        IRegulacaoFormularioService Form, IRegulacaoProcedimentoBuscaService Catalogo)
        Montar(SmsMaisDbContext db, Guid usuarioId, Guid unidadeAtiva, Guid versaoFormularioId)
    {
        // Usuário e unidade ativa reais: o escopo é fail-closed e, sem vínculo, o serviço recusa
        // — o que é o comportamento certo e é exercitado de verdade aqui.
        var acessor = new UsuarioAtualAccessorFake(usuarioId, unidadeAtiva);
        var config = new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), acessor);
        var exigencias = new RegulacaoExigenciaService(db, new StoreFake(), config, acessor);
        var pacientes = Substitute.For<IPacientesService>();
        var form = Substitute.For<IRegulacaoFormularioService>();

        // A versão precisa EXISTIR: `regulacao_solicitacao.formulario_versao_id` tem FK real, e
        // um id inventado derruba o insert com violação de chave estrangeira.
        form.ObterOuGerarAsync(Arg.Any<Guid>(), Arg.Any<FluxoRegulacao>(), Arg.Any<CancellationToken>())
            .Returns(new RegulacaoFormularioDto(versaoFormularioId, "externo.uniao", []));
        form.ObrigatoriosFaltando(Arg.Any<RegulacaoFormularioDto>(), Arg.Any<JsonElement>())
            .Returns([]);

        // Sem oferta interna por padrão: é o caso da maioria dos testes (procedimento que só
        // existe fora). Quem exercita a régua do R-03 redefine este dublê.
        var catalogo = Substitute.For<IRegulacaoProcedimentoBuscaService>();
        catalogo.ObterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new RegulacaoProcedimentoDetalheDto(
                ci.Arg<Guid>(), "PROC", TipoProcedimentoRegulacao.Consulta, null, [], [],
                new ExisteExternoDto(false, false, false)));

        // O serviço de eventos entra de verdade, não dublado: a trilha é parte do comportamento
        // que estes testes prendem, não uma dependência a isolar.
        var eventos = new RegulacaoEventoService(db, acessor);

        return (
            new RegulacaoSolicitacaoService(db, acessor, form, exigencias, config, catalogo, eventos, pacientes),
            pacientes, form, catalogo);
    }

    /// <summary>Faz o procedimento ter oferta em Maricá — o gatilho da régua do R-03.</summary>
    private static void ComOfertaInterna(IRegulacaoProcedimentoBuscaService catalogo, string unidade) =>
        catalogo.ObterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new RegulacaoProcedimentoDetalheDto(
                ci.Arg<Guid>(), "CONSULTA EM CARDIOLOGIA", TipoProcedimentoRegulacao.Consulta, null, [],
                [new ExecutanteInternoDto(Guid.NewGuid(), unidade, "1234567", 20, null)],
                new ExisteExternoDto(true, false, false)));

    private static void PacienteDublado(IPacientesService pacientes, Guid id, string nome, string? cpf)
    {
        pacientes.ObterPorIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(PacienteDtoFabrica.Criar(id, nome, cpf, "700000000000000"));
    }

    private static async Task<(Guid UnidadeId, Guid ProcedimentoId, Guid UsuarioId, Guid VersaoFormularioId)>
        CenarioAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID SOLIC {Sufixo()}",
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);

        var p = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC SOLIC {Sufixo()}",
            NomeNormalizado = "PROC SOLIC",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(p);

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "SOLICITANTE TESTE",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = usuario.Id,
            UnidadeId = unidade.Id,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var versao = new RegulacaoFormularioVersao
        {
            Id = Guid.CreateVersion7(),
            Esquema = "externo.uniao",
            ProcedimentoId = p.Id,
            DefinicaoJson = "[]",
            Hash = Guid.NewGuid().ToString("N"),
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoFormularioVersoes.Add(versao);
        await db.SaveChangesAsync();

        return (unidade.Id, p.Id, usuario.Id, versao.Id);
    }

    [Fact]
    public async Task Criar_nasce_rascunho_com_a_caixinha_de_anexos_gerais()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "MARIA", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        s.Status.Should().Be(StatusRegulacao.Rascunho);
        s.NumeroLocal.Should().BeGreaterThan(0, "o número é gerado pelo banco");
        s.PacienteNome.Should().Be("MARIA");

        // Os anexos precisam de dono antes de o solicitante chegar ao passo do formulário.
        (await db.RegulacaoSolicitacaoExigencias.CountAsync(e => e.SolicitacaoId == s.Id && e.RegraId == null))
            .Should().Be(1);
    }

    [Fact]
    public async Task Nar_sem_unidade_em_nome_de_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "JOAO", "52998224725");

        var acao = () => servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Nar, procedimentoId, pacienteId, null, null, null),
            CancellationToken.None);

        // Sem a unidade "em nome de", o agente não tem com qual credencial incluir no SISREG (D-9).
        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Nar_forca_o_destino_para_sisreg()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "ANA", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Nar, procedimentoId, pacienteId, unidadeId, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        s.SistemaDestino.Should().Be(SistemaRegulacao.Sisreg, "o NAR é sempre agendamento indireto no SISREG");
    }

    [Fact]
    public async Task Fluxo_normal_nao_aceita_unidade_em_nome_de()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "ANA", "52998224725");

        var acao = () => servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, unidadeId, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Sem_cpf_a_solicitacao_salva_mas_nao_vai_para_a_fila()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "SEM CPF", null);

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Sernit, null),
            CancellationToken.None);

        // Salvar rascunho funciona (ADR-0041: paciente sem CPF entra marcado, não fica de fora).
        s.Status.Should().Be(StatusRegulacao.Rascunho);

        var pendencias = await servico.PendenciasDeEnvioAsync(s.Id, CancellationToken.None);
        pendencias.Should().Contain(p => p.Codigo == "paciente.cpf");

        // O SERNIT não grava sem CPF: falhar aqui, com nome, é melhor do que falhar no envio.
        var acao = () => servico.EnviarParaFilaAsync(s.Id, CancellationToken.None);
        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Sem_destino_nao_vai_para_a_fila()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, null, null),
            CancellationToken.None);

        (await servico.PendenciasDeEnvioAsync(s.Id, CancellationToken.None))
            .Should().Contain(p => p.Codigo == "destino");
    }

    [Fact]
    public async Task Campo_obrigatorio_faltando_vira_pendencia_com_o_rotulo()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, form, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var versao = new RegulacaoFormularioDto(versaoId, "externo.uniao", [
            new CampoFormularioDto("queixa_principal", "Queixa Principal", "textarea", true, null,
                [SistemaRegulacao.Ser], 0),
        ]);
        form.ObterOuGerarAsync(Arg.Any<Guid>(), Arg.Any<FluxoRegulacao>(), Arg.Any<CancellationToken>())
            .Returns(versao);
        form.ObrigatoriosFaltando(Arg.Any<RegulacaoFormularioDto>(), Arg.Any<JsonElement>())
            .Returns(["queixa_principal"]);

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        var pendencias = await servico.PendenciasDeEnvioAsync(s.Id, CancellationToken.None);

        // A pendência cita o RÓTULO, não a chave técnica: quem lê é quem preenche.
        pendencias.Should().Contain(p => p.Descricao.Contains("Queixa Principal"));
    }

    [Fact]
    public async Task Sem_pendencia_vai_para_a_fila()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        var depois = await servico.EnviarParaFilaAsync(s.Id, CancellationToken.None);

        depois.Status.Should().Be(StatusRegulacao.PendenteRegulacao);
        depois.NumeroExterno.Should().BeNull(
            "D-11: nada foi escrito em sistema externo — o número só existe depois do envio real");
    }

    [Fact]
    public async Task Ja_na_fila_nao_e_mais_editavel_pela_ponta()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);
        await servico.EnviarParaFilaAsync(s.Id, CancellationToken.None);

        var acao = () => servico.AtualizarAsync(
            s.Id, new AtualizarRegulacaoSolicitacaoRequest(null, null, "tentando editar"),
            CancellationToken.None);

        await acao.Should().ThrowAsync<ConflitoException>();
    }

    [Fact]
    public async Task Atualizar_guarda_o_formulario_sob_canonico()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        var canonico = JsonDocument.Parse("""{"queixa_principal":"dor no peito"}""").RootElement;
        var depois = await servico.AtualizarAsync(
            s.Id, new AtualizarRegulacaoSolicitacaoRequest(null, canonico, null), CancellationToken.None);

        depois.Formulario.GetProperty("queixa_principal").GetString().Should().Be("dor no peito");
    }

    [Fact]
    public async Task Cancelar_depois_de_em_analise_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        var linha = await db.RegulacaoSolicitacoes.FirstAsync(x => x.Id == s.Id);
        linha.Status = StatusRegulacao.EmAnalise;
        await db.SaveChangesAsync();

        var acao = () => servico.CancelarAsync(s.Id, "desisti", CancellationToken.None);

        // Depois de o agente assumir, a ponta não puxa o tapete de quem está trabalhando no caso.
        await acao.Should().ThrowAsync<ConflitoException>();
    }

    [Fact]
    public async Task A_trilha_registra_a_criacao_e_a_ida_para_a_fila()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, _) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);
        await servico.EnviarParaFilaAsync(s.Id, CancellationToken.None);

        var trilha = await servico.EventosAsync(s.Id, CancellationToken.None);

        trilha.Select(e => e.Tipo).Should().ContainInOrder(
            TipoEventoRegulacao.Criacao, TipoEventoRegulacao.EnvioFila);

        var envio = trilha.Last(e => e.Tipo == TipoEventoRegulacao.EnvioFila);
        envio.De.Should().Be(StatusRegulacao.Rascunho);
        envio.Para.Should().Be(StatusRegulacao.PendenteRegulacao);
        envio.Papel.Should().Be(PapelEventoRegulacao.Solicitante);

        // O nome é copiado no momento do evento: renomear o usuário depois não reescreve a
        // história.
        envio.UsuarioNome.Should().Be("SOLICITANTE TESTE");
    }

    [Fact]
    public async Task Editar_grava_o_diff_e_editar_igual_nao_grava_nada()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, _) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        var antes = JsonDocument.Parse("""{"queixa":"dor no peito"}""").RootElement;
        await servico.AtualizarAsync(
            s.Id, new AtualizarRegulacaoSolicitacaoRequest(null, antes, null), CancellationToken.None);

        var depois = JsonDocument.Parse("""{"queixa":"dor no peito ha 3 dias"}""").RootElement;
        await servico.AtualizarAsync(
            s.Id, new AtualizarRegulacaoSolicitacaoRequest(null, depois, null), CancellationToken.None);

        // Salvar o MESMO conteúdo não vira linha: "editou" sem dizer o quê é ruído que esconde
        // as edições que importam.
        await servico.AtualizarAsync(
            s.Id, new AtualizarRegulacaoSolicitacaoRequest(null, depois, null), CancellationToken.None);

        var edicoes = (await servico.EventosAsync(s.Id, CancellationToken.None))
            .Where(e => e.Tipo == TipoEventoRegulacao.Edicao).ToList();

        edicoes.Should().HaveCount(2);
        var diff = edicoes[1].Diff!.Value.GetProperty("queixa");
        diff.GetProperty("de").GetString().Should().Be("dor no peito");
        diff.GetProperty("para").GetString().Should().Be("dor no peito ha 3 dias");
    }

    [Fact]
    public async Task Cancelar_depois_de_assumido_recusa_pela_maquina_de_estados()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, _) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");

        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        var linha = await db.RegulacaoSolicitacoes.FirstAsync(x => x.Id == s.Id);
        linha.Status = StatusRegulacao.EmAnalise;
        await db.SaveChangesAsync();

        var acao = () => servico.CancelarAsync(s.Id, "desisti", CancellationToken.None);

        var erro = await acao.Should().ThrowAsync<ConflitoException>();
        erro.Which.Codigo.Should().Be("regulacao.transicao_invalida");
    }

    [Fact]
    public async Task Externo_com_oferta_interna_e_recusado_quando_a_config_nao_permite()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");
        ComOfertaInterna(catalogo, "CDT MARICA");

        var acao = () => servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
            CancellationToken.None);

        // `PermitirExternoComInterno` nasce false: havendo vaga em Maricá, o normal é resolver
        // dentro do município. A trava é do serviço — a tela só esconder o cartão não segura um
        // POST direto.
        var erro = await acao.Should().ThrowAsync<ValidacaoException>();
        erro.Which.Erros.Should().ContainKey("fluxo");
        erro.Which.Erros["fluxo"][0].Should().Contain("CDT MARICA");
    }

    [Fact]
    public async Task Externo_com_oferta_interna_passa_quando_a_config_permite()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");
        ComOfertaInterna(catalogo, "CDT MARICA");

        var config = await db.RegulacaoConfiguracoes
            .FirstOrDefaultAsync(c => c.Id == RegulacaoConfiguracao.IdSingleton);
        if (config is null)
        {
            config = new RegulacaoConfiguracao();
            db.RegulacaoConfiguracoes.Add(config);
        }
        config.PermitirExternoComInterno = true;
        await db.SaveChangesAsync();
        try
        {
            var s = await servico.CriarAsync(
                new CriarRegulacaoSolicitacaoRequest(
                    FluxoRegulacao.Externo, procedimentoId, pacienteId, null, SistemaRegulacao.Ser, null),
                CancellationToken.None);

            s.Fluxo.Should().Be(FluxoRegulacao.Externo);
        }
        finally
        {
            // O singleton é compartilhado por toda a bancada: deixá-lo ligado mudaria a régua
            // dos outros testes.
            config.PermitirExternoComInterno = false;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Nar_com_oferta_interna_nao_passa_pela_regua_do_externo()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, procedimentoId, usuarioId, versaoId) = await CenarioAsync(db);
        var (servico, pacientes, _, catalogo) = Montar(db, usuarioId, unidadeId, versaoId);
        var pacienteId = Guid.NewGuid();
        PacienteDublado(pacientes, pacienteId, "COM CPF", "52998224725");
        ComOfertaInterna(catalogo, "CDT MARICA");

        var outra = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID EM NOME DE {Sufixo()}",
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(outra);
        await db.SaveChangesAsync();

        // O NAR é sempre SISREG, em nome de outra unidade — a régua "não mande para fora havendo
        // oferta interna" não se aplica a ele.
        var s = await servico.CriarAsync(
            new CriarRegulacaoSolicitacaoRequest(
                FluxoRegulacao.Nar, procedimentoId, pacienteId, outra.Id, null, null),
            CancellationToken.None);

        s.Fluxo.Should().Be(FluxoRegulacao.Nar);
        s.SistemaDestino.Should().Be(SistemaRegulacao.Sisreg);
    }
}
