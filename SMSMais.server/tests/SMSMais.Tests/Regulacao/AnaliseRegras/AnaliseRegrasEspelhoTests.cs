using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Regulacao.AnaliseRegras;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.AnaliseRegras;

/// <summary>
/// A análise automática das regras sobre os pedidos que CHEGAM dos espelhos (ADR-0063 §4).
///
/// <para>O que prendem: o pedido acha o procedimento canônico pelo RÓTULO da origem do mesmo
/// sistema; a regra dedutível decide com o que o espelho sabe (aqui, a idade); a segunda passada
/// sem mudança não regrava; regra editada muda o hash e a passada seguinte reanalisa sozinha; e
/// pedido sem origem no catálogo é "sem procedimento", não "apto".</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AnaliseRegrasEspelhoTests(PostgresFixture fixture)
{
    private static AnaliseRegrasEspelhoService Servico(SmsMaisDbContext db) => new(
        db,
        new RegulacaoConfiguracaoService(db, new MemoryCache(new MemoryCacheOptions()), new UsuarioAtualAccessorFake()),
        NullLogger<AnaliseRegrasEspelhoService>.Instance);

    private static async Task<(string Rotulo, Guid ProcedimentoId, Guid RegraId)> CatalogoComRegraAsync(SmsMaisDbContext db)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var rotulo = $"TRATAMENTO TESTE {sufixo} (PPI)";
        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = rotulo,
            NomeNormalizado = rotulo,
            Tipo = TipoProcedimentoRegulacao.Exame,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoProcedimentos.Add(procedimento);
        db.RegulacaoProcedimentoOrigens.Add(new RegulacaoProcedimentoOrigem
        {
            Id = Guid.CreateVersion7(),
            ProcedimentoId = procedimento.Id,
            Sistema = SistemaRegulacao.EsusSg,
            ChaveExterna = $"2|{sufixo}",
            RotuloExterno = rotulo,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        });
        var regra = new RegulacaoRegra
        {
            Id = Guid.CreateVersion7(),
            ProcedimentoId = procedimento.Id,
            Tipo = TipoRegraRegulacao.Dedutivel,
            Severidade = SeveridadeRegraRegulacao.Bloqueia,
            Descricao = "Só para maiores de 18 anos",
            IdadeMinAnos = 18,
            Versao = 1,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoRegras.Add(regra);
        await db.SaveChangesAsync();
        return (rotulo, procedimento.Id, regra.Id);
    }

    private static EsusSgSolicitacao Pedido(string recurso, DateOnly nascimento) => new()
    {
        Id = Guid.NewGuid(),
        IdEsusSg = Random.Shared.NextInt64(10_000_000, 99_999_999).ToString(),
        Tipo = TipoRecursoEsusSg.Exame,
        Recurso = recurso,
        PacienteNome = "PACIENTE ANALISE",
        DataNascimento = nascimento,
        Sexo = "F",
        Situacao = SituacaoEsusSg.EmFila,
        CriadoEm = DateTime.UtcNow,
        SincronizadoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task Regra_de_idade_bloqueia_crianca_e_libera_adulto_pelo_rotulo_da_origem()
    {
        await using var db = fixture.CriarDbContext();
        var (rotulo, procedimentoId, _) = await CatalogoComRegraAsync(db);
        var crianca = Pedido(rotulo, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10));
        // Rótulo com caixa/espaço diferentes: casa pela chave normalizada, como o resto da regulação.
        var adulto = Pedido("  " + rotulo.ToLowerInvariant() + " ", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-40));
        db.EsusSgSolicitacoes.AddRange(crianca, adulto);
        await db.SaveChangesAsync();

        await Servico(db).AnalisarPendentesAsync(SistemaRegulacao.EsusSg, 100_000, CancellationToken.None);

        var aCrianca = await db.RegulacaoAnalisesEspelho.AsNoTracking()
            .FirstAsync(a => a.Sistema == SistemaRegulacao.EsusSg && a.EspelhoId == crianca.Id);
        Assert.Equal(VereditoAnaliseRegras.Bloqueado, aCrianca.Veredito);
        Assert.Equal(procedimentoId, aCrianca.ProcedimentoId);
        Assert.Equal(1, aCrianca.Bloqueios);
        Assert.False(string.IsNullOrWhiteSpace(aCrianca.Resumo));

        var aAdulto = await db.RegulacaoAnalisesEspelho.AsNoTracking()
            .FirstAsync(a => a.Sistema == SistemaRegulacao.EsusSg && a.EspelhoId == adulto.Id);
        Assert.Equal(VereditoAnaliseRegras.Apto, aAdulto.Veredito);
    }

    [Fact]
    public async Task Sem_mudanca_nao_regrava_e_regra_editada_reanalisa()
    {
        await using var db = fixture.CriarDbContext();
        var (rotulo, _, regraId) = await CatalogoComRegraAsync(db);
        var pedido = Pedido(rotulo, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10));
        db.EsusSgSolicitacoes.Add(pedido);
        await db.SaveChangesAsync();

        var servico = Servico(db);
        await servico.AnalisarPendentesAsync(SistemaRegulacao.EsusSg, 100_000, CancellationToken.None);
        var primeira = await db.RegulacaoAnalisesEspelho.AsNoTracking().FirstAsync(a => a.EspelhoId == pedido.Id);

        await servico.AnalisarPendentesAsync(SistemaRegulacao.EsusSg, 100_000, CancellationToken.None);
        var segunda = await db.RegulacaoAnalisesEspelho.AsNoTracking().FirstAsync(a => a.EspelhoId == pedido.Id);
        Assert.Equal(primeira.AnalisadoEm, segunda.AnalisadoEm);

        // A tela de regras edita: idade mínima cai para 5 anos (nova versão).
        var regra = await db.RegulacaoRegras.FirstAsync(r => r.Id == regraId);
        regra.IdadeMinAnos = 5;
        regra.Versao = 2;
        regra.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await Servico(db).AnalisarPendentesAsync(SistemaRegulacao.EsusSg, 100_000, CancellationToken.None);
        var terceira = await db.RegulacaoAnalisesEspelho.AsNoTracking().FirstAsync(a => a.EspelhoId == pedido.Id);
        Assert.Equal(VereditoAnaliseRegras.Apto, terceira.Veredito);
        Assert.NotEqual(primeira.EntradaHash, terceira.EntradaHash);
    }

    /// <summary>
    /// Documento não decide o parecer (09/10/2026): a análise não enxerga os anexos do pedido, e a
    /// regra do encaminhamento — que todo procedimento do SER tem — deixava tudo "a conferir".
    /// Pergunta que pode barrar continua deixando "a conferir".
    /// </summary>
    [Fact]
    public async Task Documento_fica_listado_mas_nao_decide_e_pergunta_que_barra_fica_a_conferir()
    {
        await using var db = fixture.CriarDbContext();
        var (rotulo, procedimentoId, regraIdade) = await CatalogoComRegraAsync(db);
        // Só o documento: a regra de idade sai de cena.
        (await db.RegulacaoRegras.FirstAsync(r => r.Id == regraIdade)).Ativo = false;
        db.RegulacaoRegras.Add(new RegulacaoRegra
        {
            Id = Guid.CreateVersion7(),
            ProcedimentoId = procedimentoId,
            Tipo = TipoRegraRegulacao.Documental,
            Severidade = SeveridadeRegraRegulacao.Bloqueia,
            Descricao = "Encaminhamento médico com a descrição clara e detalhada do caso.",
            DocumentoRotulo = "Encaminhamento médico",
            Obrigatorio = true,
            Versao = 1,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        });
        var pedido = Pedido(rotulo, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-40));
        db.EsusSgSolicitacoes.Add(pedido);
        await db.SaveChangesAsync();

        var servico = Servico(db);
        await servico.AnalisarPendentesAsync(SistemaRegulacao.EsusSg, 100_000, CancellationToken.None);
        var soDocumento = await db.RegulacaoAnalisesEspelho.AsNoTracking().FirstAsync(a => a.EspelhoId == pedido.Id);
        Assert.Equal(VereditoAnaliseRegras.Apto, soDocumento.Veredito);
        Assert.Equal(1, soDocumento.DocumentosPendentes);
        Assert.Contains("Encaminhamento médico", soDocumento.Resumo);
        Assert.Contains("ESUS", soDocumento.Resumo);

        db.RegulacaoRegras.Add(new RegulacaoRegra
        {
            Id = Guid.CreateVersion7(),
            ProcedimentoId = procedimentoId,
            Tipo = TipoRegraRegulacao.NaoDedutivel,
            Severidade = SeveridadeRegraRegulacao.Bloqueia,
            Descricao = "Critérios de exclusão: Doenças psiquiátricas descompensada.",
            Pergunta = "O paciente tem doença psiquiátrica descompensada?",
            RespostaBloqueia = RespostaRegraRegulacao.Sim,
            Versao = 1,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await servico.AnalisarPendentesAsync(SistemaRegulacao.EsusSg, 100_000, CancellationToken.None);
        var comPergunta = await db.RegulacaoAnalisesEspelho.AsNoTracking().FirstAsync(a => a.EspelhoId == pedido.Id);
        Assert.Equal(VereditoAnaliseRegras.AConferir, comPergunta.Veredito);
        Assert.StartsWith("Conferir: O paciente tem doença psiquiátrica", comPergunta.Resumo);
    }

    [Fact]
    public async Task Pedido_sem_origem_no_catalogo_e_sem_procedimento_nao_apto()
    {
        await using var db = fixture.CriarDbContext();
        var pedido = Pedido($"PROCEDIMENTO QUE NAO EXISTE {Guid.NewGuid():N}", new DateOnly(1980, 1, 1));
        db.EsusSgSolicitacoes.Add(pedido);
        await db.SaveChangesAsync();

        var detalhe = await Servico(db).ReanalisarAsync(SistemaRegulacao.EsusSg, pedido.Id, CancellationToken.None);

        Assert.NotNull(detalhe);
        Assert.Equal(VereditoAnaliseRegras.SemProcedimento, detalhe.Resumo.Veredito);
        Assert.Null(detalhe.ProcedimentoId);
    }

    [Theory]
    [InlineData("J353 - Hipertrofia das amígdalas", "J35.3")]
    [InlineData("C50.4 Neoplasia", "C50.4")]
    [InlineData("H40", "H40")]
    [InlineData("sem cid", null)]
    [InlineData(null, null)]
    public void Cid_do_espelho_vira_codigo_pontuado(string? cid, string? esperado) =>
        Assert.Equal(esperado, AnaliseRegrasEspelhoService.CodigoCid(cid));
}
