using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Regulacao.Catalogo.Dtos;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Regras;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Solicitacoes;

/// <summary>
/// A fila e o escopo (tarefa 3.3).
///
/// <para>O que prendem, e por quê: a unidade vê <b>só o que é dela</b> — vazar solicitação entre
/// unidades expõe a que serviço um paciente foi encaminhado, que é dado clínico; o <b>agente
/// regulador vê tudo</b>, porque a fila de triagem é do município e não de uma unidade; e quem
/// não tem vínculo nenhum <b>não vê nada</b> (fail-closed, ADR-0037) — até 2026-07-30 a postura
/// era o inverso, e a ausência de configuração era a permissão mais ampla do sistema.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoFilaEscopoTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private sealed class StoreFake : IArquivoExigenciaStore
    {
        public string MontarChave(Guid p, Guid a, string e) => $"Regulacao/{p:D}/{a:D}.{e}";
        public Task SalvarAsync(string c, byte[] b, CancellationToken ct) => Task.CompletedTask;
        public Task<byte[]?> LerAsync(string c, CancellationToken ct) => Task.FromResult<byte[]?>(null);
        public Task ExcluirAsync(string c, CancellationToken ct) => Task.CompletedTask;
    }

    /// <param name="EhAgente">Se o usuário tem o módulo 48 — é o que decide "vejo tudo".</param>
    /// <param name="camposDoFormulario">
    /// Chaves que o formulário dublado declara. Importa em um teste só — o da troca de
    /// procedimento, que precisa de uma chave que sobrevive e outra que não.
    /// </param>
    /// <param name="obrigatorios">
    /// Chaves que o formulário dublado exige — com o mesmo critério do serviço real (ausente ou em
    /// branco = faltando). Sem elas, nada falta.
    /// </param>
    private static RegulacaoSolicitacaoService Montar(
        SmsMaisDbContext db, Guid usuarioId, Guid? unidadeAtiva, Guid versaoFormularioId,
        bool ehAgente, IPacientesService? pacientes = null, string[]? camposDoFormulario = null,
        string[]? obrigatorios = null, CampoFormularioDto[]? campos = null)
    {
        var acessor = new UsuarioAtualAccessorFake(usuarioId, unidadeAtiva);
        var config = new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), acessor);
        pacientes ??= Substitute.For<IPacientesService>();

        var form = Substitute.For<IRegulacaoFormularioService>();
        campos ??= [.. (camposDoFormulario ?? [])
            .Select((chave, i) => new CampoFormularioDto(chave, chave, "text", false, null, [], i))];
        form.ObterOuGerarAsync(Arg.Any<Guid>(), Arg.Any<FluxoRegulacao>(), Arg.Any<CancellationToken>())
            .Returns(new RegulacaoFormularioDto(versaoFormularioId, "externo.uniao", campos));
        form.ObrigatoriosFaltando(Arg.Any<RegulacaoFormularioDto>(), Arg.Any<JsonElement>())
            .Returns(ci => FaltandoEntre(obrigatorios ?? [], ci.Arg<JsonElement>()));

        var catalogo = Substitute.For<IRegulacaoProcedimentoBuscaService>();
        catalogo.ObterAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new RegulacaoProcedimentoDetalheDto(
                ci.Arg<Guid>(), "PROC", TipoProcedimentoRegulacao.Consulta, null, [], [],
                new ExisteExternoDto(false, false, false)));

        // O módulo 48 vem do serviço de identidade — dublado aqui porque montar perfil e
        // permissão no banco a cada teste esconderia o que está sob teste, que é o escopo.
        var identidade = Substitute.For<IIdentidadeService>();
        identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PermissoesResolvidasDto(
                Herdadas: [],
                Overrides: [],
                Resolvidas: ehAgente
                    ? [new PermissaoModuloDto(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]
                    : []));

        var escopo = new RegulacaoEscopo(db, acessor, identidade);
        var exigencias = new RegulacaoExigenciaService(db, new StoreFake(), config, acessor, escopo);
        var eventos = new RegulacaoEventoService(db, acessor);

        // As regras do manual têm teste próprio (PendenciasDasRegrasTests). Aqui o procedimento não
        // tem regra: a avaliação volta vazia e a conferência do envio segue como antes.
        var elegibilidade = Substitute.For<IRegulacaoElegibilidadeService>();
        elegibilidade.AvaliarAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new AvaliacaoElegibilidadeDto(
                [], [], [], [], [], new Dictionary<SistemaRegulacao, string>(), false));

        return new RegulacaoSolicitacaoService(
            db, acessor, form, exigencias, config, catalogo, eventos, escopo, pacientes,
            elegibilidade);
    }

    private static IReadOnlyList<string> FaltandoEntre(string[] obrigatorios, JsonElement canonico) =>
        [.. obrigatorios.Where(chave => canonico.ValueKind != JsonValueKind.Object
            || !canonico.TryGetProperty(chave, out var v)
            || string.IsNullOrWhiteSpace(v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()))];

    /// <summary>
    /// Uma versão mais nova do formulário do mesmo procedimento — o que acontece quando o catálogo
    /// muda depois da abertura (o bloco fixo do SER entrou assim, em 01/10/2026).
    /// </summary>
    private static async Task<Guid> VersaoMaisNovaAsync(SmsMaisDbContext db, Guid procedimentoId)
    {
        var versao = new RegulacaoFormularioVersao
        {
            Id = Guid.CreateVersion7(),
            Esquema = "externo.uniao",
            ProcedimentoId = procedimentoId,
            DefinicaoJson = "[]",
            Hash = Guid.NewGuid().ToString("N"),
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoFormularioVersoes.Add(versao);
        await db.SaveChangesAsync();
        return versao.Id;
    }

    /// <summary>O formulário como a unidade deixou ao enviar para a fila.</summary>
    private static async Task PreencherAsync(SmsMaisDbContext db, Guid solicitacaoId, string canonico)
    {
        var s = await db.RegulacaoSolicitacoes.FirstAsync(x => x.Id == solicitacaoId);
        s.FormularioJson = $$"""{"canonico":{{canonico}}}""";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private sealed record Cenario(
        Guid UnidadeA, Guid UnidadeB, Guid UsuarioA, Guid UsuarioB, Guid Agente,
        Guid ProcedimentoId, Guid VersaoId, Guid SolicitacaoA, Guid SolicitacaoB);

    /// <summary>Duas unidades, um usuário em cada, um agente sem vínculo, e uma solicitação de cada lado.</summary>
    private static async Task<Cenario> CenarioAsync(SmsMaisDbContext db)
    {
        var sufixo = Sufixo();
        var unidadeA = new Unidade { Id = Guid.NewGuid(), Nome = $"UNID A {sufixo}", CriadoEm = DateTime.UtcNow };
        var unidadeB = new Unidade { Id = Guid.NewGuid(), Nome = $"UNID B {sufixo}", CriadoEm = DateTime.UtcNow };
        db.Unidades.AddRange(unidadeA, unidadeB);

        Usuario NovoUsuario(string nome) => new()
        {
            Id = Guid.NewGuid(),
            NomeCompleto = nome,
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        var usuarioA = NovoUsuario($"PONTA A {sufixo}");
        var usuarioB = NovoUsuario($"PONTA B {sufixo}");
        var agente = NovoUsuario($"AGENTE {sufixo}");
        db.Usuarios.AddRange(usuarioA, usuarioB, agente);

        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC FILA {sufixo}",
            NomeNormalizado = "PROC FILA",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(procedimento);
        await db.SaveChangesAsync();

        db.UsuarioUnidades.AddRange(
            new UsuarioUnidade { UsuarioId = usuarioA.Id, UnidadeId = unidadeA.Id, Principal = true, CriadoEm = DateTime.UtcNow },
            new UsuarioUnidade { UsuarioId = usuarioB.Id, UnidadeId = unidadeB.Id, Principal = true, CriadoEm = DateTime.UtcNow });

        var versao = new RegulacaoFormularioVersao
        {
            Id = Guid.CreateVersion7(),
            Esquema = "externo.uniao",
            ProcedimentoId = procedimento.Id,
            DefinicaoJson = "[]",
            Hash = Guid.NewGuid().ToString("N"),
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoFormularioVersoes.Add(versao);

        RegulacaoSolicitacao Nova(Guid unidadeId, Guid autorId, string paciente) => new()
        {
            Id = Guid.CreateVersion7(),
            Fluxo = FluxoRegulacao.Externo,
            UnidadeSolicitanteId = unidadeId,
            CriadoPorUsuarioId = autorId,
            PacienteId = Guid.NewGuid(),
            PacienteNome = paciente,
            PacienteCpf = "52998224725",
            ProcedimentoId = procedimento.Id,
            SistemaDestino = SistemaRegulacao.Ser,
            FormularioVersaoId = versao.Id,
            FormularioJson = """{"canonico":{}}""",
            Status = StatusRegulacao.PendenteRegulacao,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = autorId,
        };

        var solA = Nova(unidadeA.Id, usuarioA.Id, $"PACIENTE DA A {sufixo}");
        var solB = Nova(unidadeB.Id, usuarioB.Id, $"PACIENTE DA B {sufixo}");
        db.RegulacaoSolicitacoes.AddRange(solA, solB);
        await db.SaveChangesAsync();

        return new Cenario(
            unidadeA.Id, unidadeB.Id, usuarioA.Id, usuarioB.Id, agente.Id,
            procedimento.Id, versao.Id, solA.Id, solB.Id);
    }

    [Fact]
    public async Task A_unidade_ve_a_propria_solicitacao_e_nao_a_da_outra()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var servico = Montar(db, c.UsuarioA, c.UnidadeA, c.VersaoId, ehAgente: false);

        var pagina = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId), CancellationToken.None);

        var ids = pagina.Itens.Select(i => i.Id).ToList();
        ids.Should().Contain(c.SolicitacaoA);
        ids.Should().NotContain(c.SolicitacaoB,
            "vazar entre unidades expõe a que serviço um paciente foi encaminhado");
    }

    [Fact]
    public async Task O_agente_ve_as_duas_unidades()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // Agente sem vínculo nenhum em usuario_unidade — é o módulo 48 que o autoriza, e é assim
        // que ele é na vida real: a regulação não é lotada nas UBS.
        var servico = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);

        var pagina = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId, FilaDoMunicipio: true),
            CancellationToken.None);

        var ids = pagina.Itens.Select(i => i.Id).ToList();
        ids.Should().Contain(c.SolicitacaoA).And.Contain(c.SolicitacaoB);
    }

    [Fact]
    public async Task Na_fila_da_unidade_o_agente_ve_so_a_unidade_escolhida_no_topo()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // Agente lotado na unidade A, com ela escolhida no topo. Na tela "Solicitações" a fila é a
        // da unidade — pré-regulação, em análise, devolvidas e encerradas das outras unidades não
        // aparecem ali; elas são da "Gestão de fila".
        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = c.Agente, UnidadeId = c.UnidadeA, Principal = true, CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var servico = Montar(db, c.Agente, c.UnidadeA, c.VersaoId, ehAgente: true);

        var daUnidade = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId), CancellationToken.None);
        daUnidade.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA).And.NotContain(c.SolicitacaoB);

        // A fila do agente também segue a unidade do topo: com a A escolhida, é a da A.
        var filaDoAgente = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId, FilaDoMunicipio: true),
            CancellationToken.None);
        filaDoAgente.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA).And.NotContain(c.SolicitacaoB);

        // "Todas" no topo (nenhuma unidade escolhida): o município.
        var semUnidade = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        var doMunicipio = await semUnidade.ListarAsync(
            new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId, FilaDoMunicipio: true),
            CancellationToken.None);
        doMunicipio.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA).And.Contain(c.SolicitacaoB);

        // A fila é filtro, não permissão: o agente escolhe a unidade B no topo mesmo sem vínculo
        // com ela, e o detalhe segue aberto a qualquer caso.
        var naB = Montar(db, c.Agente, c.UnidadeB, c.VersaoId, ehAgente: true);
        var filaDaB = await naB.ListarAsync(
            new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId, FilaDoMunicipio: true),
            CancellationToken.None);
        filaDaB.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoB).And.NotContain(c.SolicitacaoA);
        var detalhe = await servico.ObterAsync(c.SolicitacaoB, CancellationToken.None);
        detalhe.Id.Should().Be(c.SolicitacaoB);
    }

    [Fact]
    public async Task Pedir_a_fila_do_municipio_sem_ser_agente_nao_amplia_nada()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var servico = Montar(db, c.UsuarioA, c.UnidadeA, c.VersaoId, ehAgente: false);

        var pagina = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId, FilaDoMunicipio: true),
            CancellationToken.None);

        pagina.Itens.Select(i => i.Id).Should().NotContain(c.SolicitacaoB);
    }

    [Fact]
    public async Task Rascunho_so_aparece_para_quem_o_abriu()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // Um colega na MESMA unidade e o agente: nenhum dos dois vê o rascunho do usuário A.
        var colega = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "COLEGA DA A",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(colega);
        await db.SaveChangesAsync();
        db.UsuarioUnidades.Add(new UsuarioUnidade
        {
            UsuarioId = colega.Id, UnidadeId = c.UnidadeA, Principal = true, CriadoEm = DateTime.UtcNow,
        });
        await db.RegulacaoSolicitacoes.Where(s => s.Id == c.SolicitacaoA)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.Status, StatusRegulacao.Rascunho));
        await db.SaveChangesAsync();

        var filtro = new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId, FilaDoMunicipio: true);

        var doAutor = await Montar(db, c.UsuarioA, c.UnidadeA, c.VersaoId, ehAgente: false)
            .ListarAsync(filtro, CancellationToken.None);
        doAutor.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA);

        var servicoDoColega = Montar(db, colega.Id, c.UnidadeA, c.VersaoId, ehAgente: false);
        var doColega = await servicoDoColega.ListarAsync(filtro, CancellationToken.None);
        doColega.Itens.Select(i => i.Id).Should().NotContain(c.SolicitacaoA,
            "rascunho é trabalho em andamento de uma pessoa, não da unidade");

        var resumoDoColega = await servicoDoColega.ResumoAsync(false, CancellationToken.None);
        resumoDoColega.PorStatus.GetValueOrDefault(StatusRegulacao.Rascunho).Should().Be(0);

        var servicoDoAgente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        var doAgente = await servicoDoAgente.ListarAsync(filtro, CancellationToken.None);
        doAgente.Itens.Select(i => i.Id).Should().NotContain(c.SolicitacaoA);

        // Nem pela porta do detalhe, nem pela da linha do tempo.
        var detalheDoColega = () => servicoDoColega.ObterAsync(c.SolicitacaoA, CancellationToken.None);
        await detalheDoColega.Should().ThrowAsync<NaoEncontradoException>();
        var trilhaDoAgente = () => servicoDoAgente.EventosAsync(c.SolicitacaoA, CancellationToken.None);
        await trilhaDoAgente.Should().ThrowAsync<NaoEncontradoException>();
    }

    [Fact]
    public async Task Devolvida_a_unidade_troca_procedimento_e_paciente_e_reenvia()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        var outro = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC CORRIGIDO {Sufixo()}",
            NomeNormalizado = "PROC CORRIGIDO",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(outro);
        await db.SaveChangesAsync();

        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.DevolverAsync(c.SolicitacaoA, "Paciente e procedimento errados.", CancellationToken.None);

        var novoPaciente = Guid.NewGuid();
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(novoPaciente, Arg.Any<CancellationToken>())
            .Returns(PacienteDtoFabrica.Criar(novoPaciente, "PACIENTE CERTO", "11144477735", null));
        var ponta = Montar(db, c.UsuarioA, c.UnidadeA, c.VersaoId, ehAgente: false, pacientes: pacientes);

        var depois = await ponta.AtualizarAsync(
            c.SolicitacaoA,
            new AtualizarRegulacaoSolicitacaoRequest(
                SistemaRegulacao.Sernit, null, null, ProcedimentoId: outro.Id, PacienteId: novoPaciente),
            CancellationToken.None);

        depois.Status.Should().Be(StatusRegulacao.Devolvida);
        depois.ProcedimentoId.Should().Be(outro.Id);
        depois.PacienteId.Should().Be(novoPaciente);
        depois.PacienteNome.Should().Be("PACIENTE CERTO");
        depois.PacienteCpf.Should().Be("11144477735");
        depois.SistemaDestino.Should().Be(SistemaRegulacao.Sernit);

        // A correção fica na trilha, dita: quem era, quem passou a ser.
        var edicao = (await ponta.EventosAsync(c.SolicitacaoA, CancellationToken.None))
            .Should().ContainSingle(e => e.Tipo == TipoEventoRegulacao.Edicao).Subject;
        edicao.Papel.Should().Be(PapelEventoRegulacao.Solicitante);
        edicao.Diff!.Value.TryGetProperty("paciente", out _).Should().BeTrue();
        edicao.Diff!.Value.TryGetProperty("procedimento", out _).Should().BeTrue();

        var reenviada = await ponta.EnviarParaFilaAsync(c.SolicitacaoA, CancellationToken.None);
        reenviada.Status.Should().Be(StatusRegulacao.PendenteRegulacao);
    }

    [Fact]
    public async Task O_agente_nao_troca_paciente_pela_edicao()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var acao = () => agente.AtualizarAsync(
            c.SolicitacaoA,
            new AtualizarRegulacaoSolicitacaoRequest(null, null, null, PacienteId: Guid.NewGuid()),
            CancellationToken.None);

        var erro = await acao.Should().ThrowAsync<ConflitoException>();
        erro.Which.Codigo.Should().Be("regulacao.solicitacao.estrutura_da_unidade");
    }

    [Fact]
    public async Task Sem_vinculo_e_sem_o_modulo_do_agente_a_fila_vem_vazia()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        var semNada = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "SEM VINCULO",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(semNada);
        await db.SaveChangesAsync();

        var servico = Montar(db, semNada.Id, null, c.VersaoId, ehAgente: false);

        var pagina = await servico.ListarAsync(new RegulacaoSolicitacaoFiltro(), CancellationToken.None);

        // Fail-closed (ADR-0037): sem vínculo NÃO É "vê tudo".
        pagina.Total.Should().Be(0);
        pagina.Itens.Should().BeEmpty();
    }

    [Fact]
    public async Task Detalhe_de_outra_unidade_responde_nao_encontrado_e_nao_sem_permissao()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var servico = Montar(db, c.UsuarioA, c.UnidadeA, c.VersaoId, ehAgente: false);

        var acao = () => servico.ObterAsync(c.SolicitacaoB, CancellationToken.None);

        // "Não encontrado" e não "sem permissão": dizer que existe já vaza que aquele paciente
        // tem solicitação naquela unidade.
        await acao.Should().ThrowAsync<NaoEncontradoException>();
    }

    [Fact]
    public async Task A_busca_acha_por_nome_e_por_numero_local()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var servico = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);

        var numero = await db.RegulacaoSolicitacoes.AsNoTracking()
            .Where(s => s.Id == c.SolicitacaoA).Select(s => s.NumeroLocal).FirstAsync();
        var nome = await db.RegulacaoSolicitacoes.AsNoTracking()
            .Where(s => s.Id == c.SolicitacaoA).Select(s => s.PacienteNome).FirstAsync();

        // Busca pelo número local, qualquer que seja o tamanho dele. Este teste falhou no CI e
        // passou na bancada: lá os números já estão altos de execuções anteriores, e o código
        // exigia 3 dígitos para considerar número. Num município novo, a solicitação 7 é a 7.
        var porNumero = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(Busca: numero.ToString(), FilaDoMunicipio: true),
            CancellationToken.None);
        porNumero.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA);

        // Busca por nome é case-insensitive: quem digita no balcão não usa caixa alta.
        var porNome = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(Busca: nome.ToLowerInvariant(), FilaDoMunicipio: true),
            CancellationToken.None);
        porNome.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA);
    }

    [Fact]
    public async Task Dois_agentes_assumindo_juntos_um_ganha_e_o_outro_recebe_conflito()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // Dois DbContexts distintos: é o que reproduz duas requisições concorrentes de verdade.
        // Com um só, o change tracker mascararia a corrida — os dois veriam a mesma instância.
        await using var db2 = fixture.CriarDbContext();
        var primeiro = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        var segundo = Montar(db2, c.UsuarioB, null, c.VersaoId, ehAgente: true);

        await primeiro.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var acao = () => segundo.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        var erro = await acao.Should().ThrowAsync<ConflitoException>();
        erro.Which.Codigo.Should().Be("regulacao.ja_assumida");

        // O caso ficou com quem chegou primeiro — e só com ele.
        var linha = await db2.RegulacaoSolicitacoes.AsNoTracking()
            .FirstAsync(x => x.Id == c.SolicitacaoA);
        linha.Status.Should().Be(StatusRegulacao.EmAnalise);
        linha.AgenteResponsavelId.Should().Be(c.Agente);
    }

    [Fact]
    public async Task Assumir_registra_a_transicao_na_trilha()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);

        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var trilha = await agente.EventosAsync(c.SolicitacaoA, CancellationToken.None);
        var assumida = trilha.Should().ContainSingle(e => e.Tipo == TipoEventoRegulacao.Assumida).Subject;
        assumida.De.Should().Be(StatusRegulacao.PendenteRegulacao);
        assumida.Para.Should().Be(StatusRegulacao.EmAnalise);
        assumida.Papel.Should().Be(PapelEventoRegulacao.Agente);
    }

    [Fact]
    public async Task Devolver_e_recusar_exigem_motivo()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        // Devolver sem motivo faria a unidade receber o caso de volta sem saber o que corrigir.
        var semMotivo = () => agente.DevolverAsync(c.SolicitacaoA, "   ", CancellationToken.None);
        await semMotivo.Should().ThrowAsync<ValidacaoException>();

        var semMotivoRecusa = () => agente.RecusarAsync(c.SolicitacaoA, "", CancellationToken.None);
        await semMotivoRecusa.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Devolver_volta_para_a_unidade_com_o_motivo_visivel()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        var assumida = await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        assumida.Status.Should().Be(StatusRegulacao.EmAnalise);

        var depois = await agente.DevolverAsync(
            c.SolicitacaoA, "Falta o laudo do exame anterior.", CancellationToken.None);

        depois.Status.Should().Be(StatusRegulacao.Devolvida);
        depois.StatusMotivo.Should().Be("Falta o laudo do exame anterior.");

        // E a unidade consegue reenviar: Devolvida volta para a fila pela mão do solicitante.
        MaquinaDeEstadosRegulacao
            .PodeTransitar(StatusRegulacao.Devolvida, StatusRegulacao.PendenteRegulacao,
                PapelEventoRegulacao.Solicitante)
            .Should().BeTrue();
    }

    [Fact]
    public async Task Quem_nao_e_agente_nao_assume()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // Usuário da própria unidade, que ENXERGA a solicitação — mas sem o módulo 48. Sem esta
        // trava, a ponta assumiria o próprio caso e a triagem viraria decoração.
        var daPonta = Montar(db, c.UsuarioA, c.UnidadeA, c.VersaoId, ehAgente: false);

        var acao = () => daPonta.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await acao.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task O_ajuste_do_agente_entra_na_trilha_como_Ajuste_e_nao_como_Edicao()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var valores = JsonDocument.Parse("""{"queixa":"corrigido pelo agente"}""").RootElement;
        await agente.AtualizarAsync(
            c.SolicitacaoA,
            new AtualizarRegulacaoSolicitacaoRequest(null, valores, null),
            CancellationToken.None);

        var trilha = await agente.EventosAsync(c.SolicitacaoA, CancellationToken.None);

        // A distinção importa na leitura da linha do tempo: "a unidade corrigiu" e "o agente
        // corrigiu por ela" são fatos diferentes.
        trilha.Should().ContainSingle(e => e.Tipo == TipoEventoRegulacao.Ajuste);
        trilha.Should().NotContain(e => e.Tipo == TipoEventoRegulacao.Edicao);
    }

    [Fact]
    public async Task Registrar_envio_grava_o_numero_e_marca_como_assistido()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var numero = $"SER{Sufixo()}";
        var depois = await agente.RegistrarEnvioAsync(
            c.SolicitacaoA,
            new RegistrarEnvioRequest(SistemaRegulacao.Ser, numero, null),
            CancellationToken.None);

        depois.Status.Should().Be(StatusRegulacao.EnviadaAoSistema);
        depois.NumeroExterno.Should().Be(numero);
        depois.EnvioAssistido.Should().BeTrue("o número foi digitado pelo agente, não gerado por nós");

        var evento = (await agente.EventosAsync(c.SolicitacaoA, CancellationToken.None))
            .Should().ContainSingle(e => e.Tipo == TipoEventoRegulacao.NumeroExterno).Subject;
        evento.Detalhe!.Value.GetProperty("assistido").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task O_mesmo_numero_externo_nao_entra_em_duas_solicitacoes()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);

        var numero = $"SER{Sufixo()}";
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.RegistrarEnvioAsync(
            c.SolicitacaoA, new RegistrarEnvioRequest(SistemaRegulacao.Ser, numero, null),
            CancellationToken.None);

        await agente.AssumirAsync(c.SolicitacaoB, CancellationToken.None);
        var acao = () => agente.RegistrarEnvioAsync(
            c.SolicitacaoB, new RegistrarEnvioRequest(SistemaRegulacao.Ser, numero, null),
            CancellationToken.None);

        // O índice único parcial é a trava real: sem ela, um duplo clique (ou dois agentes
        // lançando o mesmo número) faria dois casos nossos apontarem para o mesmo pedido lá fora,
        // e a conciliação escolheria um deles em silêncio.
        var erro = await acao.Should().ThrowAsync<ConflitoException>();
        erro.Which.Codigo.Should().Be("regulacao.numero_externo_duplicado");
    }

    [Fact]
    public async Task Registrar_envio_exige_numero()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var acao = () => agente.RegistrarEnvioAsync(
            c.SolicitacaoA, new RegistrarEnvioRequest(SistemaRegulacao.Ser, "   ", null),
            CancellationToken.None);
        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task O_ok_interno_e_so_do_fluxo_interno()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);

        // O cenário é Externo: o "OK, já está no SISREG" não faz sentido aqui, e deixá-lo passar
        // tiraria da fila um caso que ninguém incluiu em lugar nenhum.
        var acao = () => agente.ConfirmarOkInternoAsync(c.SolicitacaoA, CancellationToken.None);
        await acao.Should().ThrowAsync<ValidacaoException>();
    }

    [Fact]
    public async Task Trocar_procedimento_preserva_o_que_sobrevive_e_registra_o_que_cai()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // Um segundo procedimento canônico: é entre eles que o agente desempata (D-10 — o balde
        // e o específico são entradas distintas, e a troca vale nos dois sentidos).
        var outro = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC DESTINO {Sufixo()}",
            NomeNormalizado = "PROC DESTINO",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(outro);
        await db.SaveChangesAsync();

        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true,
            camposDoFormulario: ["queixa"]);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        // `queixa` existe no formulário novo; `sobrevive_nao` não.
        var valores = JsonDocument
            .Parse("""{"queixa":"dor no peito","sobrevive_nao":"valor antigo"}""").RootElement;
        await agente.AtualizarAsync(
            c.SolicitacaoA, new AtualizarRegulacaoSolicitacaoRequest(null, valores, null),
            CancellationToken.None);

        var depois = await agente.TrocarProcedimentoAsync(
            c.SolicitacaoA, outro.Id, CancellationToken.None);

        depois.ProcedimentoId.Should().Be(outro.Id);
        depois.Formulario.GetProperty("queixa").GetString().Should().Be("dor no peito",
            "o que o formulário novo ainda pede não se redigita");
        depois.Formulario.TryGetProperty("sobrevive_nao", out _).Should().BeFalse(
            "resposta de campo que não existe mais ficaria órfã");

        // O que caiu tem de aparecer na trilha: informação perdida na troca é diferente de
        // informação esquecida.
        var evento = (await agente.EventosAsync(c.SolicitacaoA, CancellationToken.None))
            .Should().ContainSingle(e => e.Tipo == TipoEventoRegulacao.TrocaProcedimento).Subject;
        evento.Diff!.Value.TryGetProperty("sobrevive_nao", out _).Should().BeTrue();
        evento.Detalhe!.Value.GetProperty("respostasPreservadas").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Trocar_procedimento_e_recusado_depois_do_numero_externo()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);

        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.RegistrarEnvioAsync(
            c.SolicitacaoA, new RegistrarEnvioRequest(SistemaRegulacao.Ser, $"SER{Sufixo()}", null),
            CancellationToken.None);

        // O pedido já existe lá fora com aquele procedimento: trocar aqui faria a nossa ficha
        // divergir do sistema de regulação, sem ninguém saber qual das duas está certa.
        var acao = () => agente.TrocarProcedimentoAsync(
            c.SolicitacaoA, c.ProcedimentoId, CancellationToken.None);
        var erro = await acao.Should().ThrowAsync<ConflitoException>();
        erro.Which.Codigo.Should().Be("regulacao.procedimento_travado");
    }

    [Fact]
    public async Task O_resumo_conta_no_escopo_de_quem_pergunta()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        var daPonta = await Montar(db, c.UsuarioA, c.UnidadeA, c.VersaoId, ehAgente: false)
            .ResumoAsync(false, CancellationToken.None);
        var doAgente = await Montar(db, c.Agente, null, c.VersaoId, ehAgente: true)
            .ResumoAsync(true, CancellationToken.None);

        daPonta.VeTodasUnidades.Should().BeFalse();
        doAgente.VeTodasUnidades.Should().BeTrue();

        var pendentesDaPonta = daPonta.PorStatus.GetValueOrDefault(StatusRegulacao.PendenteRegulacao);
        var pendentesDoAgente = doAgente.PorStatus.GetValueOrDefault(StatusRegulacao.PendenteRegulacao);

        pendentesDaPonta.Should().BeGreaterThanOrEqualTo(1);
        pendentesDoAgente.Should().BeGreaterThan(pendentesDaPonta,
            "o agente enxerga as duas unidades do cenário, a ponta só a dela");
    }

    // ------------------------------------------------------------ envio automático ao SER (07/10/2026)

    [Fact]
    public async Task Envio_automatico_trava_em_Enviando_e_o_segundo_inicio_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        await agente.IniciarEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);
        (await agente.ObterAsync(c.SolicitacaoA, CancellationToken.None)).Status
            .Should().Be(StatusRegulacao.EnviandoAoSistema);

        // Duplo clique, ou dois agentes: o segundo não pode começar outro envio do mesmo caso.
        var deNovo = () => agente.IniciarEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);
        await deNovo.Should().ThrowAsync<ConflitoException>();
    }

    [Fact]
    public async Task Envio_automatico_concluido_grava_numero_quem_assinou_e_se_conferiu()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.IniciarEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);

        var numero = $"9{Random.Shared.Next(1_000_000, 9_999_999)}";
        var depois = await agente.ConcluirEnvioAutomaticoAsync(
            c.SolicitacaoA,
            new ConclusaoEnvioSer(numero, "regulador.ser", Conferido: true, "Solicitação salva com sucesso", []),
            CancellationToken.None);

        depois.Status.Should().Be(StatusRegulacao.EnviadaAoSistema);
        depois.NumeroExterno.Should().Be(numero);
        depois.EnvioAssistido.Should().BeFalse("o número veio do nosso envio, não da digitação do agente");

        var evento = (await agente.EventosAsync(c.SolicitacaoA, CancellationToken.None))
            .Last(e => e.Tipo == TipoEventoRegulacao.NumeroExterno);
        evento.Detalhe!.Value.GetProperty("automatico").GetBoolean().Should().BeTrue();
        evento.Detalhe!.Value.GetProperty("conferido").GetBoolean().Should().BeTrue();
        evento.Detalhe!.Value.GetProperty("operadorSer").GetString().Should().Be("regulador.ser");
    }

    [Fact]
    public async Task Falha_depois_do_Gravar_avisa_para_conferir_e_aceita_o_numero_digitado()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.IniciarEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);

        var falhou = await agente.RegistrarFalhaEnvioAsync(
            c.SolicitacaoA, "O SER não devolveu o número.", gravarAcionado: true, CancellationToken.None);

        falhou.Status.Should().Be(StatusRegulacao.FalhaEnvio);
        falhou.StatusMotivo.Should().StartWith("ATENÇÃO: o Gravar chegou a ser enviado ao SER");

        // Conferiu no SER e achou o pedido: registra o número sem passar por "Em análise".
        var numero = $"SER{Sufixo()}";
        var depois = await agente.RegistrarEnvioAsync(
            c.SolicitacaoA, new RegistrarEnvioRequest(SistemaRegulacao.Ser, numero, null), CancellationToken.None);
        depois.Status.Should().Be(StatusRegulacao.EnviadaAoSistema);
        depois.NumeroExterno.Should().Be(numero);
        depois.StatusMotivo.Should().BeNull("o aviso era da falha; com o número registrado ele confunde (PR-17)");
    }

    [Fact]
    public async Task Falha_antes_do_Gravar_diz_que_nada_foi_gravado_e_permite_reenviar()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.IniciarEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);

        var falhou = await agente.RegistrarFalhaEnvioAsync(
            c.SolicitacaoA, "O SER não aceita o CID X99.", gravarAcionado: false, CancellationToken.None);
        falhou.StatusMotivo.Should().StartWith("Nada foi gravado no SER.");

        await agente.IniciarEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);
        (await agente.ObterAsync(c.SolicitacaoA, CancellationToken.None)).Status
            .Should().Be(StatusRegulacao.EnviandoAoSistema);
    }

    [Fact]
    public async Task Envio_automatico_nao_comeca_em_solicitacao_que_ninguem_assumiu()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);

        var preparar = () => agente.PrepararEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);
        await preparar.Should().ThrowAsync<ConflitoException>();
    }

    // ------------------------------------------------- ajuste do regulador e versão do formulário (09/10/2026)
    //
    // PR-17: aberta antes do bloco fixo do SER, guardava a versão do formulário sem Classificação de
    // risco. A unidade preencheu o risco (a tela já pedia), e o "Enviar ao SER" respondeu "a
    // solicitação está sem Classificação de risco" — a tradução usava a versão da abertura.

    [Fact]
    public async Task O_regulador_ajusta_risco_e_CID_e_a_trilha_diz_o_que_mudou_e_quem_mudou()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        await PreencherAsync(db, c.SolicitacaoA,
            """{"classificacao_risco":"URGENCIA","hipotese_cid":"(C34 ) Neoplasia","observacoes":"dispneia"}""");
        var nova = await VersaoMaisNovaAsync(db, c.ProcedimentoId);
        var agente = Montar(db, c.Agente, null, nova, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var ajustado = JsonDocument.Parse(
            """{"classificacao_risco":"EMERGENCIA","hipotese_cid":"(C341) Lobo superior","observacoes":"dispneia"}""").RootElement;
        var depois = await agente.AtualizarAsync(
            c.SolicitacaoA, new AtualizarRegulacaoSolicitacaoRequest(null, ajustado, null), CancellationToken.None);

        depois.Formulario.GetProperty("classificacao_risco").GetString().Should().Be("EMERGENCIA");
        depois.FormularioVersaoId.Should().Be(nova,
            "a versão gravada é a que a tela desenhou agora, não a da abertura");

        var ajuste = (await agente.EventosAsync(c.SolicitacaoA, CancellationToken.None))
            .Should().ContainSingle(e => e.Tipo == TipoEventoRegulacao.Ajuste).Subject;
        ajuste.Papel.Should().Be(PapelEventoRegulacao.Agente);
        ajuste.UsuarioNome.Should().StartWith("AGENTE ", "a trilha diz qual técnico mudou");
        var risco = ajuste.Diff!.Value.GetProperty("classificacao_risco");
        risco.GetProperty("de").GetString().Should().Be("URGENCIA");
        risco.GetProperty("para").GetString().Should().Be("EMERGENCIA");
        ajuste.Diff!.Value.GetProperty("hipotese_cid").GetProperty("para").GetString().Should().Be("(C341) Lobo superior");
        ajuste.Diff!.Value.TryGetProperty("observacoes", out _).Should().BeFalse("o que não mudou não entra na trilha");
    }

    [Fact]
    public async Task O_regulador_nao_deixa_em_branco_campo_que_a_unidade_foi_obrigada_a_preencher()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        await PreencherAsync(db, c.SolicitacaoA, """{"classificacao_risco":"URGENCIA","hipotese_cid":"(C34 ) Neoplasia"}""");
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true,
            obrigatorios: ["classificacao_risco", "hipotese_cid"]);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var semRisco = JsonDocument.Parse("""{"classificacao_risco":"","hipotese_cid":"(C34 ) Neoplasia"}""").RootElement;
        var acao = () => agente.AtualizarAsync(
            c.SolicitacaoA, new AtualizarRegulacaoSolicitacaoRequest(null, semRisco, null), CancellationToken.None);

        (await acao.Should().ThrowAsync<ValidacaoException>()).Which.Message.Should().Contain("classificacao_risco");
        (await agente.ObterAsync(c.SolicitacaoA, CancellationToken.None))
            .Formulario.GetProperty("classificacao_risco").GetString().Should().Be("URGENCIA");
    }

    [Fact]
    public async Task Depois_de_uma_falha_no_envio_o_regulador_ainda_ajusta()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.IniciarEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.RegistrarFalhaEnvioAsync(
            c.SolicitacaoA, "O SER não aceita o CID C34 para este recurso.", gravarAcionado: false, CancellationToken.None);

        // É depois da recusa do CID que o regulador mais precisa trocar — sem devolver à unidade.
        var outroCid = JsonDocument.Parse("""{"hipotese_cid":"(C349) Pulmao, nao especificado"}""").RootElement;
        var depois = await agente.AtualizarAsync(
            c.SolicitacaoA, new AtualizarRegulacaoSolicitacaoRequest(null, outroCid, null), CancellationToken.None);

        depois.Status.Should().Be(StatusRegulacao.FalhaEnvio);
        depois.Formulario.GetProperty("hipotese_cid").GetString().Should().Be("(C349) Pulmao, nao especificado");
        (await agente.EventosAsync(c.SolicitacaoA, CancellationToken.None))
            .Should().Contain(e => e.Tipo == TipoEventoRegulacao.Ajuste);
    }

    [Fact]
    public async Task Enviar_para_a_fila_grava_a_versao_do_formulario_que_acabou_de_ser_conferida()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);
        await agente.DevolverAsync(c.SolicitacaoA, "Falta o CID.", CancellationToken.None);

        var nova = await VersaoMaisNovaAsync(db, c.ProcedimentoId);
        var ponta = Montar(db, c.UsuarioA, c.UnidadeA, nova, ehAgente: false);
        var reenviada = await ponta.EnviarParaFilaAsync(c.SolicitacaoA, CancellationToken.None);

        reenviada.Status.Should().Be(StatusRegulacao.PendenteRegulacao);
        reenviada.FormularioVersaoId.Should().Be(nova);
    }

    [Fact]
    public async Task O_envio_traduz_pela_versao_mais_nova_e_a_grava_ao_comecar()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);

        // O procedimento do cenário precisa de um recurso do SER para o envio ter para onde ir.
        var recurso = new SMSMais.Data.Entities.Ser.SerCatalogoRecurso
        {
            Id = Guid.NewGuid(), Tipo = SMSMais.Data.Entities.Ser.TipoRecursoSer.Consulta,
            Valor = $"9{Sufixo()[..4]}", Rotulo = $"CONSULTA TESTE ENVIO {Sufixo()}",
            SincronizadoEm = DateTime.UtcNow, CamposLidos = true,
        };
        db.SerCatalogoRecursos.Add(recurso);
        db.RegulacaoProcedimentoOrigens.Add(new RegulacaoProcedimentoOrigem
        {
            Id = Guid.CreateVersion7(), ProcedimentoId = c.ProcedimentoId, Sistema = SistemaRegulacao.Ser,
            ChaveExterna = $"1|{recurso.Valor}|NAO_AE", RotuloExterno = recurso.Rotulo, Ramo = "NAO_AE",
            SerCatalogoRecursoId = recurso.Id, Ativo = true, CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var nova = await VersaoMaisNovaAsync(db, c.ProcedimentoId);
        var agente = Montar(db, c.Agente, null, nova, ehAgente: true);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var dados = await agente.PrepararEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);
        dados.FormularioVersaoId.Should().Be(nova, "a versão da abertura não conhece o bloco fixo");

        await agente.IniciarEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None, dados.FormularioVersaoId);
        (await agente.ObterAsync(c.SolicitacaoA, CancellationToken.None)).FormularioVersaoId
            .Should().Be(nova, "a versão que de fato foi fica gravada na solicitação");
    }

    [Fact]
    public async Task O_envio_diz_o_que_falta_em_vez_de_descartar_o_campo_na_traducao()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        await PreencherAsync(db, c.SolicitacaoA, """{"hipotese_cid":"(C34 ) Neoplasia"}""");
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true,
            obrigatorios: ["classificacao_risco", "hipotese_cid"]);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var preparar = () => agente.PrepararEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);

        var erro = await preparar.Should().ThrowAsync<ValidacaoException>();
        erro.Which.Message.Should().Contain("classificacao_risco").And.NotContain("hipotese_cid");
    }

    [Fact]
    public async Task O_envio_ao_SER_nao_para_em_campo_que_so_o_SERNIT_pede()
    {
        await using var db = fixture.CriarDbContext();
        var c = await CenarioAsync(db);
        var recurso = new SMSMais.Data.Entities.Ser.SerCatalogoRecurso
        {
            Id = Guid.NewGuid(), Tipo = SMSMais.Data.Entities.Ser.TipoRecursoSer.Consulta,
            Valor = $"9{Sufixo()[..4]}", Rotulo = $"CONSULTA TESTE UNIAO {Sufixo()}",
            SincronizadoEm = DateTime.UtcNow, CamposLidos = true,
        };
        db.SerCatalogoRecursos.Add(recurso);
        db.RegulacaoProcedimentoOrigens.Add(new RegulacaoProcedimentoOrigem
        {
            Id = Guid.CreateVersion7(), ProcedimentoId = c.ProcedimentoId, Sistema = SistemaRegulacao.Ser,
            ChaveExterna = $"1|{recurso.Valor}|NAO_AE", RotuloExterno = recurso.Rotulo, Ramo = "NAO_AE",
            SerCatalogoRecursoId = recurso.Id, Ativo = true, CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // O Externo é a união dos dois sistemas: o campo do SERNIT existe no formulário, mas não
        // vai para o SER — vazio, não impede o envio ao SER.
        var agente = Montar(db, c.Agente, null, c.VersaoId, ehAgente: true,
            obrigatorios: ["grau_histopatologico"],
            campos: [new CampoFormularioDto("grau_histopatologico", "Grau", "text", true, null, [SistemaRegulacao.Sernit], 0)]);
        await agente.AssumirAsync(c.SolicitacaoA, CancellationToken.None);

        var dados = await agente.PrepararEnvioAutomaticoAsync(c.SolicitacaoA, CancellationToken.None);
        dados.Sistema.Should().Be(SistemaRegulacao.Ser);
    }
}
