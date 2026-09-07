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
    private static RegulacaoSolicitacaoService Montar(
        SmsMaisDbContext db, Guid usuarioId, Guid? unidadeAtiva, Guid versaoFormularioId,
        bool ehAgente, IPacientesService? pacientes = null)
    {
        var acessor = new UsuarioAtualAccessorFake(usuarioId, unidadeAtiva);
        var config = new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), acessor);
        var exigencias = new RegulacaoExigenciaService(db, new StoreFake(), config, acessor);
        pacientes ??= Substitute.For<IPacientesService>();

        var form = Substitute.For<IRegulacaoFormularioService>();
        form.ObterOuGerarAsync(Arg.Any<Guid>(), Arg.Any<FluxoRegulacao>(), Arg.Any<CancellationToken>())
            .Returns(new RegulacaoFormularioDto(versaoFormularioId, "externo.uniao", []));
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

        var porNumero = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(Busca: numero.ToString()), CancellationToken.None);
        porNumero.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA);

        // Busca por nome é case-insensitive: quem digita no balcão não usa caixa alta.
        var porNome = await servico.ListarAsync(
            new RegulacaoSolicitacaoFiltro(Busca: nome.ToLowerInvariant()), CancellationToken.None);
        porNome.Itens.Select(i => i.Id).Should().Contain(c.SolicitacaoA);
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
