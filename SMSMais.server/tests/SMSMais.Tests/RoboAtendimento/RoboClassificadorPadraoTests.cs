using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.RoboAtendimento;

/// <summary>
/// Queda no ASSUNTO PADRÃO quando nada casa. Antes, "sem assunto" não era um estado neutro: o robô
/// entrava sem orientação, sem treinos, sem limiar de confiança e sem os comandos do assunto — e é
/// onde cai a maior parte do que o classificador não prevê. Estes testes prendem as três garantias
/// que fazem a rede valer: ela pega o que ninguém pegou, ela NÃO atropela quem casou, e ela some se
/// o padrão estiver desligado (senão a rede seria uma resposta errada com cara de certa).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RoboClassificadorPadraoTests(PostgresFixture fixture)
{
    private static RoboClassificador Criar(SmsMaisDbContext db) =>
        new(db, NullLogger<RoboClassificador>.Instance);

    /// <summary>Cria um assunto isolado. O nome carrega um sufixo aleatório porque a bancada guarda
    /// as linhas entre execuções e o nome é único.</summary>
    private static async Task<RoboAssunto> SemearAsync(
        SmsMaisDbContext db, string rotulo, bool padrao, bool ativo = true, string? palavraChave = null)
    {
        var a = new RoboAssunto
        {
            Id = Guid.CreateVersion7(),
            Nome = $"{rotulo} {Guid.NewGuid():N}",
            InstrucoesPersona = "Oriente e encaminhe.",
            Ativo = ativo,
            Padrao = padrao,
            Ordem = 500,
            CriadoEm = DateTime.UtcNow,
        };
        if (palavraChave is not null)
        {
            a.Condicoes.Add(new RoboAssuntoCondicao
            {
                Id = Guid.CreateVersion7(),
                Tipo = TipoCondicaoRobo.PalavraChave,
                Valor = palavraChave,
                Ativo = true,
            });
        }
        db.RoboAssuntos.Add(a);
        await db.SaveChangesAsync();
        return a;
    }

    /// <summary>A bancada acumula linhas: um padrão de outro teste faria este passar por engano.</summary>
    private static async Task LimparPadraoAsync(SmsMaisDbContext db)
    {
        foreach (var a in db.RoboAssuntos.Where(a => a.Padrao)) a.Padrao = false;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Sem_assunto_que_case_cai_no_padrao()
    {
        await using var db = fixture.CriarDbContext();
        await LimparPadraoAsync(db);
        var padrao = await SemearAsync(db, "Geral", padrao: true);

        var achado = await Criar(db).ResolverAsync(null, "texto que nao casa com nada aqui", default);

        Assert.NotNull(achado);
        Assert.Equal(padrao.Id, achado!.Id);
    }

    [Fact]
    public async Task Assunto_que_casa_vence_o_padrao()
    {
        await using var db = fixture.CriarDbContext();
        await LimparPadraoAsync(db);
        await SemearAsync(db, "Geral", padrao: true);
        // Termo único por execução: a bancada guarda os assuntos de rodadas anteriores, e uma
        // palavra comum ("cade", "laudo") casaria com um deles antes de chegar no meu.
        var termo = $"zz{Guid.NewGuid():N}";
        var especifico = await SemearAsync(db, "Especifico", padrao: false, palavraChave: termo);

        var achado = await Criar(db).ResolverAsync(null, termo, default);

        Assert.NotNull(achado);
        Assert.Equal(especifico.Id, achado!.Id);
    }

    [Fact]
    public async Task Padrao_desligado_nao_serve_de_rede()
    {
        await using var db = fixture.CriarDbContext();
        await LimparPadraoAsync(db);
        await SemearAsync(db, "Geral", padrao: true, ativo: false);

        var achado = await Criar(db).ResolverAsync(null, "texto que nao casa com nada aqui", default);

        Assert.Null(achado);
    }

    [Fact]
    public async Task Assunto_informado_explicitamente_nao_passa_pelo_classificador()
    {
        await using var db = fixture.CriarDbContext();
        await LimparPadraoAsync(db);
        await SemearAsync(db, "Geral", padrao: true);
        var escolhido = await SemearAsync(db, "Escolhido", padrao: false);

        // Texto que casaria com nada: mesmo assim o id informado manda (é o caso do desafio
        // cadastral pendente e o da simulação com assunto escolhido na tela).
        var achado = await Criar(db).ResolverAsync(escolhido.Id, "qualquer coisa", default);

        Assert.NotNull(achado);
        Assert.Equal(escolhido.Id, achado!.Id);
    }
}
