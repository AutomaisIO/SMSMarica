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
    private static RegulacaoSolicitacaoService Montar(
        SmsMaisDbContext db, Guid usuarioId, Guid? unidadeAtiva, Guid versaoFormularioId,
        bool ehAgente, IPacientesService? pacientes = null, string[]? camposDoFormulario = null)
    {
        var acessor = new UsuarioAtualAccessorFake(usuarioId, unidadeAtiva);
        var config = new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), acessor);
        var exigencias = new RegulacaoExigenciaService(db, new StoreFake(), config, acessor);
        pacientes ??= Substitute.For<IPacientesService>();

        var form = Substitute.For<IRegulacaoFormularioService>();
        var campos = (camposDoFormulario ?? [])
            .Select((chave, i) => new CampoFormularioDto(chave, chave, "text", false, null, [], i))
            .ToArray();
        form.ObterOuGerarAsync(Arg.Any<Guid>(), Arg.Any<FluxoRegulacao>(), Arg.Any<CancellationToken>())
            .Returns(new RegulacaoFormularioDto(versaoFormularioId, "externo.uniao", campos));
        form.ObrigatoriosFaltando(Arg.Any<RegulacaoFormularioDto>(), Arg.Any<JsonElement>()).Returns([]);

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
        var eventos = new RegulacaoEventoService(db, acessor);

        return new RegulacaoSolicitacaoService(
            db, acessor, form, exigencias, config, catalogo, eventos, escopo, pacientes);
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
            new RegulacaoSolicitacaoFiltro(ProcedimentoId: c.ProcedimentoId), CancellationToken.None);

        var ids = pagina.Itens.Select(i => i.Id).ToList();
        ids.Should().Contain(c.SolicitacaoA).And.Contain(c.SolicitacaoB);
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
            new RegulacaoSolicitacaoFiltro(Busca: numero.ToString()), CancellationToken.None);
        porNumero.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA);

        // Busca por nome é case-insensitive: quem digita no balcão não usa caixa alta.
        var porNome = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(Busca: nome.ToLowerInvariant()), CancellationToken.None);
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
            .ResumoAsync(CancellationToken.None);
        var doAgente = await Montar(db, c.Agente, null, c.VersaoId, ehAgente: true)
            .ResumoAsync(CancellationToken.None);

        daPonta.VeTodasUnidades.Should().BeFalse();
        doAgente.VeTodasUnidades.Should().BeTrue();

        var pendentesDaPonta = daPonta.PorStatus.GetValueOrDefault(StatusRegulacao.PendenteRegulacao);
        var pendentesDoAgente = doAgente.PorStatus.GetValueOrDefault(StatusRegulacao.PendenteRegulacao);

        pendentesDaPonta.Should().BeGreaterThanOrEqualTo(1);
        pendentesDoAgente.Should().BeGreaterThan(pendentesDaPonta,
            "o agente enxerga as duas unidades do cenário, a ponta só a dela");
    }
}
