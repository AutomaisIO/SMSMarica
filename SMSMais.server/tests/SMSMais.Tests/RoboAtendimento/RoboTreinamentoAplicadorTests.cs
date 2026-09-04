using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.RoboAtendimento.Treinamento;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.RoboAtendimento;

/// <summary>
/// O <b>desfazer</b> é a contrapartida de deixar o agente treinador aplicar correção sozinha
/// (ADR-0051): sem ele, um treino mal escrito entra no prompt do robô e só sai por edição manual.
/// Estes testes prendem as três garantias que fazem o desfazer valer — regra criada sai do prompt,
/// regra reescrita volta ao texto anterior, regra desativada volta a valer — mais a recusa de
/// aplicar uma condição de roteamento inválida, que derrubaria a classificação de TODAS as
/// mensagens (o pré-match roda antes de qualquer assunto ser escolhido).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RoboTreinamentoAplicadorTests(PostgresFixture fixture)
{
    private static async Task<RoboAssunto> SemearAssuntoAsync(SmsMaisDbContext db)
    {
        var a = new RoboAssunto
        {
            Id = Guid.CreateVersion7(),
            // A bancada guarda linhas entre execuções e o nome do assunto é único.
            Nome = $"Treinamento {Guid.NewGuid():N}",
            InstrucoesPersona = "Oriente e encaminhe.",
            Ativo = true,
            Ordem = 900,
            CriadoEm = DateTime.UtcNow,
        };
        db.RoboAssuntos.Add(a);
        await db.SaveChangesAsync();
        return a;
    }

    private static async Task<RoboTreinamentoItem> SemearItemAsync(SmsMaisDbContext db, Guid? assuntoId)
    {
        var i = new RoboTreinamentoItem
        {
            Id = Guid.CreateVersion7(),
            RoboAssuntoId = assuntoId,
            Critica = "O robô prometeu atendente num sábado.",
            Status = StatusTreinamentoRobo.Analisando,
            CriadoEm = DateTime.UtcNow,
        };
        db.RoboTreinamentoItens.Add(i);
        await db.SaveChangesAsync();
        return i;
    }

    [Fact]
    public async Task Desfazer_criacao_de_treino_tira_a_regra_do_prompt()
    {
        await using var db = fixture.CriarDbContext();
        var assunto = await SemearAssuntoAsync(db);
        var item = await SemearItemAsync(db, assunto.Id);
        var aplicador = new RoboTreinamentoAplicador(db);

        var alt = await aplicador.CriarTreinoAsync(
            item.Id, assunto.Id, TipoTreinoRobo.Dont, "Sábado",
            "Não prometa atendente fora do expediente.", "veio da crítica", default);
        await db.SaveChangesAsync();

        var criado = db.RoboAssuntoTreinos.Single(t => t.Id == alt.AlvoId);
        Assert.True(criado.Ativo);

        await aplicador.DesfazerAsync(alt, quem: null, default);
        await db.SaveChangesAsync();

        // A linha continua existindo (o histórico da alteração aponta para ela), mas sai do prompt.
        Assert.False(db.RoboAssuntoTreinos.Single(t => t.Id == alt.AlvoId).Ativo);
        Assert.NotNull(alt.DesfeitoEm);
    }

    [Fact]
    public async Task Desfazer_atualizacao_de_treino_volta_ao_texto_anterior()
    {
        await using var db = fixture.CriarDbContext();
        var assunto = await SemearAssuntoAsync(db);
        var item = await SemearItemAsync(db, assunto.Id);
        var aplicador = new RoboTreinamentoAplicador(db);

        var criacao = await aplicador.CriarTreinoAsync(
            item.Id, assunto.Id, TipoTreinoRobo.Instrucao, "Original",
            "Texto original da regra.", "semeadura", default);
        await db.SaveChangesAsync();

        var edicao = await aplicador.AtualizarTreinoAsync(
            item.Id, criacao.AlvoId, TipoTreinoRobo.Dont, "Reescrito",
            "Texto reescrito pelo agente.", "ajuste dos adversários", default);
        await db.SaveChangesAsync();

        Assert.Equal("Texto reescrito pelo agente.",
            db.RoboAssuntoTreinos.Single(t => t.Id == criacao.AlvoId).Conteudo);

        await aplicador.DesfazerAsync(edicao, quem: null, default);
        await db.SaveChangesAsync();

        var voltou = db.RoboAssuntoTreinos.Single(t => t.Id == criacao.AlvoId);
        Assert.Equal("Texto original da regra.", voltou.Conteudo);
        Assert.Equal("Original", voltou.Titulo);
        Assert.Equal(TipoTreinoRobo.Instrucao, voltou.Tipo);
        Assert.True(voltou.Ativo);
    }

    [Fact]
    public async Task Desfazer_desativacao_devolve_a_regra_ao_prompt()
    {
        await using var db = fixture.CriarDbContext();
        var assunto = await SemearAssuntoAsync(db);
        var item = await SemearItemAsync(db, assunto.Id);
        var aplicador = new RoboTreinamentoAplicador(db);

        var criacao = await aplicador.CriarTreinoAsync(
            item.Id, assunto.Id, TipoTreinoRobo.Do, null, "Regra que estava valendo.", "semeadura", default);
        await db.SaveChangesAsync();

        var remocao = await aplicador.DesativarTreinoAsync(
            item.Id, criacao.AlvoId, "conflitava com o guardrail", default);
        await db.SaveChangesAsync();
        Assert.False(db.RoboAssuntoTreinos.Single(t => t.Id == criacao.AlvoId).Ativo);

        await aplicador.DesfazerAsync(remocao, quem: null, default);
        await db.SaveChangesAsync();

        Assert.True(db.RoboAssuntoTreinos.Single(t => t.Id == criacao.AlvoId).Ativo);
    }

    [Fact]
    public async Task Desfazer_duas_vezes_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var assunto = await SemearAssuntoAsync(db);
        var item = await SemearItemAsync(db, assunto.Id);
        var aplicador = new RoboTreinamentoAplicador(db);

        var alt = await aplicador.CriarTreinoAsync(
            item.Id, assunto.Id, TipoTreinoRobo.Instrucao, null, "Uma regra.", "x", default);
        await db.SaveChangesAsync();

        await aplicador.DesfazerAsync(alt, quem: null, default);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflitoException>(
            () => aplicador.DesfazerAsync(alt, quem: null, default));
    }

    [Fact]
    public async Task Condicao_com_regex_invalida_nao_entra()
    {
        await using var db = fixture.CriarDbContext();
        var assunto = await SemearAssuntoAsync(db);
        var item = await SemearItemAsync(db, assunto.Id);
        var aplicador = new RoboTreinamentoAplicador(db);

        // "[" abre classe e não fecha: aceitar isso derrubaria o pré-match de toda mensagem.
        await Assert.ThrowsAsync<ValidacaoException>(() => aplicador.CriarCondicaoAsync(
            item.Id, assunto.Id, TipoCondicaoRobo.Regex, "[nao fecha", "roteamento", default));
    }

    [Fact]
    public async Task Condicao_repetida_no_mesmo_assunto_e_recusada()
    {
        await using var db = fixture.CriarDbContext();
        var assunto = await SemearAssuntoAsync(db);
        var item = await SemearItemAsync(db, assunto.Id);
        var aplicador = new RoboTreinamentoAplicador(db);
        var termo = $"zz{Guid.NewGuid():N}";

        await aplicador.CriarCondicaoAsync(
            item.Id, assunto.Id, TipoCondicaoRobo.PalavraChave, termo, "roteamento", default);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflitoException>(() => aplicador.CriarCondicaoAsync(
            item.Id, assunto.Id, TipoCondicaoRobo.PalavraChave, termo, "de novo", default));
    }
}
