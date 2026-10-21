using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Credenciais;

/// <summary>
/// O JSON da credencial carrega também a agenda dos motores (fila, escalas, varredura). Trocar só o
/// login não pode apagá-la — era o que acontecia com o SER/SERNIT, que salvavam com JSON nulo
/// (ADR-0076). E substituir o JSON inteiro continua possível: a coleta de indicadores retoma
/// REMOVENDO a chave de pausa.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class IntegracaoCredencialServiceTests(PostgresFixture fixture)
{
    // Provedor sem consumidor no backend: o teste pode reescrevê-lo à vontade.
    private const string Provedor = "google";

    private static IntegracaoCredencialService Servico(SmsMaisDbContext db)
    {
        var protetor = Substitute.For<IProtetorSegredos>();
        protetor.Proteger(Arg.Any<string>()).Returns(c => "cifrado:" + c.Arg<string>());
        protetor.Revelar(Arg.Any<string>()).Returns(c => c.Arg<string>().Replace("cifrado:", ""));
        return new IntegracaoCredencialService(db, protetor, new UsuarioAtualAccessorFake(Guid.NewGuid()));
    }

    private static async Task<string?> JsonGravadoAsync(SmsMaisDbContext db) =>
        (await db.IntegracaoCredenciais.AsNoTracking().SingleAsync(c => c.Provedor == Provedor)).ParametrosJson;

    // jsonb normaliza espaços e ordem: compara o conteúdo, não o texto.
    private static void MesmoJson(string esperado, string? gravado) =>
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(esperado), JsonNode.Parse(gravado!)), gravado);

    [Fact]
    public async Task Trocar_so_o_login_com_json_nulo_mantem_a_agenda_gravada()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        const string agenda = """{"baseUrl":"https://x","varreduraAtiva":true,"varreduraHoraLocal":"03:00"}""";
        await servico.AtualizarAsync(Provedor, new AtualizarIntegracaoCredencialRequest("u0", "s0", null, agenda, true));

        await servico.AtualizarAsync(Provedor, new AtualizarIntegracaoCredencialRequest("u1", "s1", null, null, true));

        MesmoJson(agenda, await JsonGravadoAsync(db));
        var ctx = await servico.ObterContextoAsync(Provedor);
        Assert.Equal("u1", ctx.ClientId);
        Assert.Equal("s1", ctx.ClientSecret);
    }

    [Fact]
    public async Task Json_preenchido_substitui_inteiro_inclusive_removendo_chave()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        await servico.AtualizarAsync(Provedor, new AtualizarIntegracaoCredencialRequest(
            null, null, null, """{"indicadoresColetaAtiva":true,"indicadoresColetaPausadaAte":"2026-10-21T10:00:00Z"}""", true));

        await servico.AtualizarAsync(Provedor, new AtualizarIntegracaoCredencialRequest(
            null, null, null, """{"indicadoresColetaAtiva":true}""", true));

        MesmoJson("""{"indicadoresColetaAtiva":true}""", await JsonGravadoAsync(db));
    }

    [Fact]
    public async Task Json_vazio_limpa()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        await servico.AtualizarAsync(Provedor, new AtualizarIntegracaoCredencialRequest(null, null, null, """{"a":1}""", true));

        await servico.AtualizarAsync(Provedor, new AtualizarIntegracaoCredencialRequest(null, null, null, "  ", true));

        Assert.Null(await JsonGravadoAsync(db));
    }
}
