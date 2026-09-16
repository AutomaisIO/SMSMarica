using System.Text.Json;

using Microsoft.Extensions.Caching.Memory;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Configuracao.Dtos;
using SMSMais.Core.Regulacao.Configuracao.Validators;
using SMSMais.Core.Regulacao.FollowUp;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Configuracao;

/// <summary>
/// Configuração singleton da Regulação (plano 09).
///
/// <para>É uma linha só, compartilhada, lida pela busca a cada tecla e escrita por quem
/// configura. Os riscos que estes testes prendem são os dessa combinação: nascer sozinha sem
/// seed em migration, não deixar duas edições simultâneas se sobrescreverem em silêncio, e não
/// vazar parâmetro de operação para a ponta.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoConfiguracaoServiceTests(PostgresFixture fixture)
{
    private static RegulacaoConfiguracaoService Servico(SmsMaisDbContext db, IMemoryCache? cache = null) =>
        new(db, cache ?? new MemoryCache(new MemoryCacheOptions()), new UsuarioAtualAccessorFake());

    private static AtualizarRegulacaoConfiguracaoRequest Requisicao(
        RegulacaoConfiguracaoDto atual, string? rotulo = null, uint? rowVersion = null) =>
        new(
            atual.PermitirExternoComInterno,
            atual.PontaPodeEscolherUnidade,
            atual.PontaPodeVerTodasUnidades,
            atual.ExigirCpf,
            rotulo ?? atual.RotuloFila,
            atual.SisregPrazoEdicaoDias,
            atual.BuscaCorteDistancia,
            atual.BuscaScoreSugestaoPareamento,
            null,
            atual.AnexoLimiteMb,
            atual.AnexoTiposPermitidos,
            atual.NaoSeiPadrao,
            rowVersion ?? atual.RowVersion);

    [Fact]
    public async Task Primeiro_obter_cria_a_linha_default()
    {
        await using var db = fixture.CriarDbContext();

        var c = await Servico(db).ObterAsync(CancellationToken.None);

        c.Should().NotBeNull();
        c.ExigirCpf.Should().BeTrue();
        c.RotuloFila.Should().NotBeEmpty();
        c.AnexoTiposPermitidos.Should().Contain("application/pdf");
        c.BuscaCorteDistancia.Should().BeInRange(0m, 1m);
    }

    [Fact]
    public async Task Atualizar_persiste_e_invalida_o_cache()
    {
        await using var db = fixture.CriarDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var servico = Servico(db, cache);

        var antes = await servico.ObterAsync(CancellationToken.None);
        var novoRotulo = $"Fila {Guid.NewGuid():N}"[..20];

        await servico.AtualizarAsync(Requisicao(antes, rotulo: novoRotulo), CancellationToken.None);

        // Pelo mesmo serviço (cache invalidado) e por um serviço novo (foi mesmo ao banco).
        (await servico.ObterAsync(CancellationToken.None)).RotuloFila.Should().Be(novoRotulo);

        await using var outroDb = fixture.CriarDbContext();
        (await Servico(outroDb).ObterAsync(CancellationToken.None)).RotuloFila.Should().Be(novoRotulo);
    }

    [Fact]
    public async Task RowVersion_divergente_conflita()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        var atual = await servico.ObterAsync(CancellationToken.None);

        // Alguém salvou antes: o cliente ainda tem a versão velha em mãos.
        var acao = () => servico.AtualizarAsync(
            Requisicao(atual, rotulo: "Qualquer", rowVersion: atual.RowVersion + 1), CancellationToken.None);

        await acao.Should().ThrowAsync<ConflitoException>();
    }

    [Fact]
    public async Task Fluxo_nao_expoe_parametro_de_operacao()
    {
        await using var db = fixture.CriarDbContext();

        var fluxo = await Servico(db).ObterFluxoAsync(CancellationToken.None);

        // O DTO do wizard não tem corte de busca, prazo do SISREG nem regras de follow-up —
        // e é isso que este teste guarda: quem mexer no DTO tem de decidir de novo, no claro.
        typeof(RegulacaoConfiguracaoFluxoDto).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(
                nameof(RegulacaoConfiguracaoFluxoDto.PermitirExternoComInterno),
                nameof(RegulacaoConfiguracaoFluxoDto.PontaPodeVerTodasUnidades),
                nameof(RegulacaoConfiguracaoFluxoDto.ExigirCpf),
                nameof(RegulacaoConfiguracaoFluxoDto.RotuloFila),
                nameof(RegulacaoConfiguracaoFluxoDto.AnexoLimiteMb),
                nameof(RegulacaoConfiguracaoFluxoDto.AnexoTiposPermitidos));

        fluxo.RotuloFila.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("""[{"categoria":"FalhaContato","padrao":"SEM CONTATO"}]""", true)]
    [InlineData("""[{"categoria":"SemVaga","padrao":"SEM (AGENDA|VAGA)"}]""", true)]
    // Categoria da taxonomia velha do plano — o spike d trocou por nove classes.
    [InlineData("""[{"categoria":"DocumentoCriticado","padrao":"ILEGIVEL"}]""", false)]
    // Regex que não compila: salva sem erro e explode depois, classificando follow-up real.
    [InlineData("""[{"categoria":"FalhaContato","padrao":"SEM CONTATO ([A-"}]""", false)]
    [InlineData("""{"categoria":"FalhaContato"}""", false)]
    // `vira_pendencia` desconhecido abriria, no incremento 6, pendência de tipo que ninguém trata.
    [InlineData("""[{"categoria":"FalhaContato","padrao":"X","vira_pendencia":"contato"}]""", true)]
    [InlineData("""[{"categoria":"FalhaContato","padrao":"X","vira_pendencia":null}]""", true)]
    [InlineData("""[{"categoria":"FalhaContato","padrao":"X","vira_pendencia":"telefone"}]""", false)]
    public void Regras_de_followup_sao_validadas(string json, bool esperadoValido)
    {
        var validador = new AtualizarRegulacaoConfiguracaoValidator();
        var req = new AtualizarRegulacaoConfiguracaoRequest(
            false, false, false, true, "Pré-regulação", 7, 0.45m, 0.85m,
            JsonDocument.Parse(json).RootElement,
            15, ["application/pdf"], NaoSeiViraRegulacao.Ressalva, 0);

        var r = validador.Validate(req);

        r.IsValid.Should().Be(esperadoValido);
    }

    [Fact]
    public async Task Testar_followup_usa_as_regras_gravadas_agora()
    {
        // A caixa de teste da tela só serve se ler o que está salvo neste instante: calibrar é
        // editar a regex, salvar e passar um texto real por ela. Se lesse a semente fixa, diria
        // "funciona" para uma regra que ninguém aplicou.
        await using var db = fixture.CriarDbContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var servico = Servico(db, cache);

        // A configuração é uma LINHA ÚNICA e a bancada é compartilhada: o estado de fábrica só
        // existe na primeira execução da vida. Este teste escreve a própria precondição — e a
        // desfaz no fim — em vez de supor que ninguém passou por aqui antes.
        //
        // A precondição NÃO pode ser "sem regra": desde 16/09/2026 uma configuração sem regra de
        // FollowUP recebe a semente na primeira leitura (a classificação persistida precisa
        // dela). Então a precondição é uma regra própria, que ninguém mais grava.
        const string regraDoTeste =
            """[{"categoria":"RegraDoTeste","ordem":1,"padrao":"NAO ATENDE","vira_pendencia":null}]""";
        await GravarRegrasAsync(servico, regraDoTeste);

        var antes = await servico.TestarFollowUpAsync("Não atende", CancellationToken.None);
        antes.Categoria.Should().Be("RegraDoTeste");   // lê o que está salvo, não a semente

        await GravarRegrasAsync(servico, SementeFollowUp.Json);

        var depois = await servico.TestarFollowUpAsync("Não atende", CancellationToken.None);

        depois.Categoria.Should().Be("FalhaContato");
        depois.ViraPendencia.Should().Be("contato");
        // O texto normalizado volta para a tela porque é assim que o classificador o enxerga —
        // sem isso, quem calibra não entende por que a regex "não pegou".
        depois.TextoNormalizado.Should().Be("NAO ATENDE");

        // Deixa a bancada como uma instância real fica: com a semente.
        await GravarRegrasAsync(servico, SementeFollowUp.Json);
    }

    /// <summary>Grava as regras de follow-up (JSON, ou <c>null</c> para nenhuma) na linha única.</summary>
    private static async Task GravarRegrasAsync(RegulacaoConfiguracaoService servico, string? json)
    {
        var atual = await servico.ObterAsync(CancellationToken.None);
        await servico.AtualizarAsync(
            Requisicao(atual) with
            {
                RegrasFollowup = JsonDocument.Parse(json ?? "[]").RootElement,
            },
            CancellationToken.None);
    }

    [Fact]
    public void A_semente_oferecida_pela_tela_passa_pelo_proprio_validador()
    {
        // Oferecer como ponto de partida um JSON que o PUT recusaria seria um convite a um 400
        // sem explicação. As duas coisas têm de concordar.
        var req = new AtualizarRegulacaoConfiguracaoRequest(
            false, false, false, true, "Pré-regulação", 7, 0.45m, 0.85m,
            JsonDocument.Parse(SementeFollowUp.Json).RootElement,
            15, ["application/pdf"], NaoSeiViraRegulacao.Ressalva, 0);

        new AtualizarRegulacaoConfiguracaoValidator().Validate(req).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("", 7, 0.45, 15, "application/pdf", false)]     // rótulo vazio
    [InlineData("Fila", 90, 0.45, 15, "application/pdf", false)] // prazo fora da faixa
    [InlineData("Fila", 7, 1.5, 15, "application/pdf", false)]   // corte fora de 0..1
    [InlineData("Fila", 7, 0.45, 0, "application/pdf", false)]   // limite de anexo inválido
    [InlineData("Fila", 7, 0.45, 15, "application/zip", false)]  // tipo não aceito
    [InlineData("Fila", 7, 0.45, 15, "image/webp", true)]
    public void Limites_da_configuracao_sao_validados(
        string rotulo, int prazo, double corte, int limiteMb, string tipo, bool esperadoValido)
    {
        var validador = new AtualizarRegulacaoConfiguracaoValidator();
        var req = new AtualizarRegulacaoConfiguracaoRequest(
            false, false, false, true, rotulo, prazo, (decimal)corte, 0.85m, null,
            limiteMb, [tipo], NaoSeiViraRegulacao.Ressalva, 0);

        validador.Validate(req).IsValid.Should().Be(esperadoValido);
    }
}
