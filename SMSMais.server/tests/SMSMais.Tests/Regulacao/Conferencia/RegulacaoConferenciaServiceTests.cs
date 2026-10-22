using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Conferencia;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Conferencia;

/// <summary>
/// A conferência do número registrado: lê lá fora, compara, grava na linha do tempo — e "vida que
/// segue". O que prende: a falha de leitura NÃO desfaz o registro nem vira erro HTTP, e a
/// conferência não muda o estado da solicitação.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoConferenciaServiceTests(PostgresFixture fixture)
{
    private sealed class LeitorFake(Func<string, PedidoNoSistema?> ler) : ILeitorPedidoExterno
    {
        public SistemaRegulacao Sistema => SistemaRegulacao.Sisreg;
        public Task<PedidoNoSistema?> LerAsync(string numero, CancellationToken ct) => Task.FromResult(ler(numero));
    }

    private static RegulacaoConferenciaService Montar(SmsMaisDbContext db, Guid agenteId, ILeitorPedidoExterno leitor)
    {
        var acessor = new UsuarioAtualAccessorFake(agenteId, null);
        var identidade = Substitute.For<IIdentidadeService>();
        identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PermissoesResolvidasDto(
                Herdadas: [], Overrides: [],
                Resolvidas: [new PermissaoModuloDto(ModuloPermissao.RegulacaoTriagem, AcoesPermissao.Edicao)]));
        return new RegulacaoConferenciaService(
            db, new RegulacaoEscopo(db, acessor, identidade), new RegulacaoEventoService(db, acessor),
            [leitor], NullLogger<RegulacaoConferenciaService>.Instance);
    }

    private static string Cnes() => Random.Shared.Next(1_000_000, 9_999_999).ToString();

    /// <summary>Um NAR já registrado no SISREG, em nome do Péricles, com o agente que o registrou.</summary>
    private static async Task<(Guid SolicitacaoId, Guid AgenteId, string CnesEmNomeDe)> CenarioAsync(SmsMaisDbContext db)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var origem = new Unidade { Id = Guid.NewGuid(), Nome = $"CONDE {sufixo}", Cnes = Cnes(), CriadoEm = DateTime.UtcNow };
        var emNomeDe = new Unidade { Id = Guid.NewGuid(), Nome = $"PERICLES {sufixo}", Cnes = Cnes(), CriadoEm = DateTime.UtcNow };
        db.Unidades.AddRange(origem, emNomeDe);

        var agente = new Usuario
        {
            Id = Guid.NewGuid(), NomeCompleto = $"AGENTE {sufixo}", Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x", Ativo = true, CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(agente);

        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(), NomeCanonico = $"MAMOGRAFIA {sufixo}", NomeNormalizado = "MAMOGRAFIA",
            Tipo = TipoProcedimentoRegulacao.Exame, CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(procedimento);
        await db.SaveChangesAsync();

        var versao = new RegulacaoFormularioVersao
        {
            Id = Guid.CreateVersion7(), Esquema = "sisreg.inclusao", ProcedimentoId = procedimento.Id,
            DefinicaoJson = "[]", Hash = Guid.NewGuid().ToString("N"), CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoFormularioVersoes.Add(versao);

        var s = new RegulacaoSolicitacao
        {
            Id = Guid.CreateVersion7(),
            Fluxo = FluxoRegulacao.Nar,
            UnidadeSolicitanteId = origem.Id,
            UnidadeEmNomeDeId = emNomeDe.Id,
            CriadoPorUsuarioId = agente.Id,
            PacienteId = Guid.NewGuid(),
            PacienteNome = "MARIA DA SILVA",
            PacienteCpf = "52998224725",
            PacienteCns = "700000000000000",
            ProcedimentoId = procedimento.Id,
            SistemaDestino = SistemaRegulacao.Sisreg,
            FormularioVersaoId = versao.Id,
            FormularioJson = """{"canonico":{"cid10":"C50.9"}}""",
            Status = StatusRegulacao.EnviadaAoSistema,
            NumeroExterno = Random.Shared.NextInt64(600_000_000, 699_999_999).ToString(),
            EnvioAssistido = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = agente.Id,
        };
        db.RegulacaoSolicitacoes.Add(s);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return (s.Id, agente.Id, emNomeDe.Cnes!);
    }

    [Fact]
    public async Task Conferir_registra_a_divergencia_na_trilha_e_nao_mexe_no_estado()
    {
        await using var db = fixture.CriarDbContext();
        var (id, agenteId, _) = await CenarioAsync(db);

        // Lançado no SISREG com o CNES de outra unidade (o login de quem lançou, não o da "em nome de").
        var servico = Montar(db, agenteId, new LeitorFake(_ => new PedidoNoSistema(
            "SOLICITAÇÃO / PENDENTE", "MARIA DA SILVA", null, "700000000000000", [], "C509",
            "OUTRA UNIDADE", "0000001", null, null, null, null)));

        var dto = await servico.ConferirAsync(id, CancellationToken.None);

        dto.Encontrado.Should().BeTrue();
        dto.Erro.Should().BeNull();
        dto.Divergencias.Should().Be(1);
        dto.Itens.Single(i => i.Campo == "unidade").Resultado.Should().Be(ResultadoItemConferencia.Diferente);

        (await db.RegulacaoSolicitacoes.AsNoTracking().SingleAsync(x => x.Id == id))
            .Status.Should().Be(StatusRegulacao.EnviadaAoSistema, "a conferência só registra — vida que segue");

        var ultima = await servico.UltimaAsync(id, CancellationToken.None);
        ultima.Should().NotBeNull();
        ultima!.Divergencias.Should().Be(1);
        ultima.Itens.Should().BeEquivalentTo(dto.Itens);
    }

    [Fact]
    public async Task Falha_de_leitura_volta_no_corpo_e_o_numero_continua_registrado()
    {
        await using var db = fixture.CriarDbContext();
        var (id, agenteId, _) = await CenarioAsync(db);
        var servico = Montar(db, agenteId, new LeitorFake(_ => throw new HttpRequestException("sessão caída")));

        var dto = await servico.ConferirAsync(id, CancellationToken.None);

        dto.Encontrado.Should().BeFalse();
        dto.Erro.Should().Contain("O número ficou registrado");
        var s = await db.RegulacaoSolicitacoes.AsNoTracking().SingleAsync(x => x.Id == id);
        s.NumeroExterno.Should().NotBeNullOrEmpty();
        s.Status.Should().Be(StatusRegulacao.EnviadaAoSistema);
        (await db.RegulacaoEventos.CountAsync(e => e.SolicitacaoId == id && e.Tipo == TipoEventoRegulacao.ConferenciaExterna))
            .Should().Be(1, "a tentativa fica na trilha, com o motivo");
    }

    [Fact]
    public async Task Numero_que_o_sistema_nao_acha_fica_como_nao_encontrado()
    {
        await using var db = fixture.CriarDbContext();
        var (id, agenteId, _) = await CenarioAsync(db);
        var servico = Montar(db, agenteId, new LeitorFake(_ => null));

        var dto = await servico.ConferirAsync(id, CancellationToken.None);

        dto.Encontrado.Should().BeFalse();
        dto.Erro.Should().BeNull();
        dto.Itens.Should().BeEmpty();
    }
}
