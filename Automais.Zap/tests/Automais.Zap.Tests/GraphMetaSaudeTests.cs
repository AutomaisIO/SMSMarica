using System.Net;
using System.Text;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Automais.Zap.Tests;

/// <summary>
/// <c>health_status</c> do número — o que diz "conta sem pagamento" antes de a primeira mensagem
/// voltar 131042 (19/10/2026). Sem rede: a Graph é um handler de mentira.
/// </summary>
public sealed class GraphMetaSaudeTests
{
    /// <summary>Formato da Meta: veredito geral + um item por elo da cadeia, com os erros de quem trava.</summary>
    private const string Bloqueado = """
        {
          "health_status": {
            "can_send_message": "BLOCKED",
            "entities": [
              { "entity_type": "PHONE_NUMBER", "id": "1190244884170604", "can_send_message": "AVAILABLE" },
              { "entity_type": "WABA", "id": "2079", "can_send_message": "BLOCKED",
                "errors": [ { "error_code": 141006, "error_description": "There is an error with the payment method.",
                              "possible_solution": "Update the payment method in Business Manager." } ] },
              { "entity_type": "BUSINESS", "id": "88", "can_send_message": "AVAILABLE" }
            ]
          },
          "id": "1190244884170604"
        }
        """;

    [Fact]
    public async Task Le_o_veredito_e_o_motivo_de_quem_trava()
    {
        var (cliente, handler) = Montar(Bloqueado);

        var r = await cliente.ObterSaudeAsync("1190244884170604");

        r.Sucesso.Should().BeTrue();
        handler.Chamadas.Single().Should().EndWith("1190244884170604?fields=health_status");
        r.Valor!.PodeEnviar.Should().Be("BLOCKED");
        var waba = r.Valor.Entidades.Single(e => e.Entidade == "WABA");
        waba.PodeEnviar.Should().Be("BLOCKED");
        waba.Codigo.Should().Be("141006");
        waba.Descricao.Should().Contain("payment method");
        waba.Solucao.Should().Contain("Business Manager");
        r.Valor.Entidades.Should().HaveCount(3);
    }

    [Fact]
    public async Task Conta_saudavel_vem_como_available_sem_problemas()
    {
        var (cliente, _) = Montar("""
            { "health_status": { "can_send_message": "AVAILABLE", "entities": [
                { "entity_type": "PHONE_NUMBER", "id": "1", "can_send_message": "AVAILABLE" } ] }, "id": "1" }
            """);

        var r = await cliente.ObterSaudeAsync("1");

        r.Valor!.PodeEnviar.Should().Be("AVAILABLE");
        r.Valor.Entidades.Should().OnlyContain(e => e.Codigo == null);
    }

    [Fact]
    public async Task Recusa_da_Graph_vira_falha_e_nao_veredito()
    {
        var (cliente, _) = Montar("""{"error":{"message":"Invalid OAuth access token","code":190}}""",
            HttpStatusCode.BadRequest);

        var r = await cliente.ObterSaudeAsync("1");

        r.Sucesso.Should().BeFalse();
        r.Erro.Should().Contain("code 190");
    }

    private static (GraphMetaClient Cliente, Handler Handler) Montar(string corpo, HttpStatusCode codigo = HttpStatusCode.OK)
    {
        var handler = new Handler(codigo, corpo);
        return (new GraphMetaClient(new HttpClient(handler), new Configuracao(), NullLogger<GraphMetaClient>.Instance), handler);
    }

    private sealed class Handler(HttpStatusCode codigo, string corpo) : HttpMessageHandler
    {
        public List<string> Chamadas { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Chamadas.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(codigo)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class Configuracao : IConfiguracaoMetaService
    {
        public Task<CredenciaisMeta> ObterAsync(CancellationToken ct = default)
            => Task.FromResult(new CredenciaisMeta(
                "app-de-teste", "segredo", "verify", "token-de-teste", "https://graph.facebook.com/v21.0/"));

        public Task<ConfiguracaoMeta> ObterBrutaAsync(CancellationToken ct = default)
            => Task.FromResult(new ConfiguracaoMeta());

        public Task SalvarAsync(AtualizarConfiguracaoMeta dados, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
