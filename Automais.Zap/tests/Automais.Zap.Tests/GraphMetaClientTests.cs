using System.Net;
using System.Text;
using Automais.Zap.Core.Meta;
using Automais.Zap.Data.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Automais.Zap.Tests;

/// <summary>
/// Testes do retrato do número na Meta — o que alimenta a tela de diagnóstico do OBA.
/// Sem banco e sem rede: a Graph é substituída por um handler de mentira.
/// </summary>
public sealed class GraphMetaClientTests
{
    /// <summary>Resposta real do número de Maricá, com os nomes de campo que a Meta usa.</summary>
    private const string RespostaCompleta = """
        {
          "official_business_account": { "oba_status": "NOT_STARTED" },
          "verified_name": "Secretaria de Saude de Marica",
          "name_status": "APPROVED",
          "display_phone_number": "+55 21 3731-5313",
          "code_verification_status": "EXPIRED",
          "quality_rating": "GREEN",
          "platform_type": "CLOUD_API",
          "status": "CONNECTED",
          "throughput": { "level": "STANDARD" },
          "is_official_business_account": false,
          "is_on_biz_app": false,
          "search_visibility": "NON_VISIBLE",
          "id": "1190244884170604"
        }
        """;

    /// <summary>Como a Graph recusa a chamada inteira quando um campo não existe na versão pedida.</summary>
    private const string ErroCampoInexistente = """
        {
          "error": {
            "message": "(#100) Tried accessing nonexisting field (search_visibility) on node type (WhatsAppBusinessPhoneNumber)",
            "code": 100,
            "type": "OAuthException"
          }
        }
        """;

    private const string RespostaMinima = """
        {
          "official_business_account": { "oba_status": "PENDING" },
          "verified_name": "Secretaria de Saude de Marica",
          "name_status": "APPROVED",
          "platform_type": "CLOUD_API",
          "status": "CONNECTED",
          "id": "1190244884170604"
        }
        """;

    [Fact]
    public async Task ObterNumero_le_todos_os_campos_que_a_tela_mostra()
    {
        var (cliente, handler) = Montar((HttpStatusCode.OK, RespostaCompleta));

        var r = await cliente.ObterNumeroAsync("1190244884170604");

        r.Sucesso.Should().BeTrue();
        var n = r.Valor!;
        n.ObaStatus.Should().Be("NOT_STARTED");
        n.ContaOficial.Should().BeFalse();
        n.NoAppBusiness.Should().BeFalse();
        n.NameStatus.Should().Be("APPROVED");
        n.Status.Should().Be("CONNECTED");
        n.QualityRating.Should().Be("GREEN");
        n.PlatformType.Should().Be("CLOUD_API");
        n.CodeVerificationStatus.Should().Be("EXPIRED");
        n.SearchVisibility.Should().Be("NON_VISIBLE");
        n.DisplayPhoneNumber.Should().Be("+55 21 3731-5313");

        // throughput vem aninhado; a tela mostra só o nível.
        n.Throughput.Should().Be("STANDARD");

        // O retrato cru é o que salva a investigação quando a Meta muda um campo.
        n.Json.Should().Contain("oba_status");

        handler.Chamadas.Should().HaveCount(1);
        handler.Chamadas[0].Should().Contain("official_business_account");
    }

    [Fact]
    public async Task ObterNumero_cai_para_os_campos_minimos_quando_a_versao_da_graph_e_velha()
    {
        var (cliente, handler) = Montar(
            (HttpStatusCode.BadRequest, ErroCampoInexistente),
            (HttpStatusCode.OK, RespostaMinima));

        var r = await cliente.ObterNumeroAsync("1190244884170604");

        // O campo acessório não pode derrubar a tela inteira.
        r.Sucesso.Should().BeTrue();
        r.Valor!.ObaStatus.Should().Be("PENDING");
        r.Valor.SearchVisibility.Should().BeNull();

        handler.Chamadas.Should().HaveCount(2);
        handler.Chamadas[0].Should().Contain("search_visibility");
        handler.Chamadas[1].Should().NotContain("search_visibility");
        handler.Chamadas[1].Should().Contain("official_business_account");
    }

    [Fact]
    public async Task ObterNumero_devolve_a_mensagem_da_meta_quando_as_duas_tentativas_falham()
    {
        var (cliente, _) = Montar(
            (HttpStatusCode.BadRequest, ErroCampoInexistente),
            (HttpStatusCode.Unauthorized, """{"error":{"message":"Invalid OAuth access token","code":190}}"""));

        var r = await cliente.ObterNumeroAsync("1190244884170604");

        r.Sucesso.Should().BeFalse();
        r.Erro.Should().Contain("Invalid OAuth access token");
    }

    [Fact]
    public async Task ObterNumero_exige_token_de_system_user()
    {
        var (cliente, handler) = MontarCom(null, (HttpStatusCode.OK, RespostaCompleta));

        var r = await cliente.ObterNumeroAsync("1190244884170604");

        r.Sucesso.Should().BeFalse();
        handler.Chamadas.Should().BeEmpty("sem credencial não se chama a Meta");
    }

    [Theory]
    [InlineData("APPROVED", "on")]
    [InlineData("PENDING", "atencao")]
    [InlineData("NOT_STARTED", "off")]
    [InlineData("REJECTED", "off")]
    public void Selo_do_oba_status_so_fica_verde_quando_aprovado(string status, string esperado)
        => ObaStatus.Selo(status).Should().Be(esperado);

    [Fact]
    public void Status_desconhecido_e_ecoado_em_vez_de_interpretado()
    {
        // Só NOT_STARTED é documentado. Inventar significado para um status novo da Meta
        // seria pior do que admitir que não se conhece.
        var texto = ObaStatus.Explicar("SOMETHING_NEW");

        texto.Should().Contain("SOMETHING_NEW");
        texto.Should().Contain("não documentado");
    }

    /// <summary>
    /// Os tres modelos com ARTE no topo de Marica, como a Graph os devolve, ao lado de um
    /// modelo de texto puro. O header_handle que vem no exemplo e um endereco interno da Meta:
    /// serve de amostra, nao de arte para reenviar.
    /// </summary>
    private const string RespostaTemplates = """
        {
          "data": [
            {
              "id": "1", "name": "confirmacao_exame", "language": "pt_BR",
              "category": "UTILITY", "status": "APPROVED",
              "components": [
                { "type": "HEADER", "format": "IMAGE",
                  "example": { "header_handle": ["https://scontent.whatsapp.net/v/exemplo.png"] } },
                { "type": "BODY", "text": "Ola {{1}}, seu exame foi agendado!",
                  "example": { "body_text": [["Sr. Joao"]] } },
                { "type": "BUTTONS", "buttons": [{ "type": "QUICK_REPLY", "text": "Nao sou essa pessoa" }] }
              ]
            },
            {
              "id": "2", "name": "laudo_disponivel", "language": "pt_BR",
              "category": "UTILITY", "status": "APPROVED",
              "components": [
                { "type": "BODY", "text": "{{1}}, o laudo do seu {{2}} de {{3}} esta pronto." }
              ]
            },
            {
              "id": "3", "name": "aviso_unidade", "language": "pt_BR",
              "category": "UTILITY", "status": "APPROVED",
              "components": [
                { "type": "HEADER", "format": "TEXT", "text": "Aviso da {{1}}",
                  "example": { "header_text": ["UBS Centro"] } },
                { "type": "BODY", "text": "Sua unidade estara fechada." }
              ]
            }
          ]
        }
        """;

    [Fact]
    public async Task ListarTemplates_diz_que_o_modelo_tem_FOTO_no_cabecalho()
    {
        var (cliente, _) = Montar((HttpStatusCode.OK, RespostaTemplates));

        var r = await cliente.ListarTemplatesAsync("2099272540984687");

        r.Sucesso.Should().BeTrue();
        var exame = r.Valor!.Single(t => t.Nome == "confirmacao_exame");

        // Sem isto, quem envia so descobre que o modelo pede imagem quando a Meta recusa (132012).
        exame.Cabecalho.Should().NotBeNull();
        exame.Cabecalho!.Formato.Should().Be("IMAGE");
        exame.Cabecalho.ExigeMidia.Should().BeTrue();
        exame.Cabecalho.Parametros.Should().Be(0);
        exame.Cabecalho.Exemplo.Should().Contain("exemplo.png");

        // O corpo continua sendo lido mesmo com o HEADER vindo antes dele.
        exame.Corpo.Should().Contain("seu exame foi agendado");
        exame.Parametros.Should().Be(1);
        exame.Exemplos.Should().ContainSingle().Which.Should().Be("Sr. Joao");
    }

    [Fact]
    public async Task ListarTemplates_modelo_sem_cabecalho_nao_inventa_um()
    {
        var (cliente, _) = Montar((HttpStatusCode.OK, RespostaTemplates));

        var r = await cliente.ListarTemplatesAsync("2099272540984687");

        var laudo = r.Valor!.Single(t => t.Nome == "laudo_disponivel");
        laudo.Cabecalho.Should().BeNull("modelo sem header nao pode ganhar componente nenhum no envio");
        laudo.Parametros.Should().Be(3);
    }

    [Fact]
    public async Task ListarTemplates_cabecalho_de_TEXTO_conta_a_variavel()
    {
        var (cliente, _) = Montar((HttpStatusCode.OK, RespostaTemplates));

        var r = await cliente.ListarTemplatesAsync("2099272540984687");

        var aviso = r.Valor!.Single(t => t.Nome == "aviso_unidade").Cabecalho!;
        aviso.Formato.Should().Be("TEXT");
        aviso.ExigeMidia.Should().BeFalse("texto nao pede arquivo, pede o valor da variavel");
        aviso.Parametros.Should().Be(1);
        aviso.Texto.Should().Be("Aviso da {{1}}");
    }

    // ---------------------------------------------------------------- apoio

    private static (GraphMetaClient Cliente, HandlerFalso Handler) Montar(
        params (HttpStatusCode Codigo, string Corpo)[] respostas)
        => MontarCom("token-de-teste", respostas);

    private static (GraphMetaClient Cliente, HandlerFalso Handler) MontarCom(
        string? tokenSistema, params (HttpStatusCode Codigo, string Corpo)[] respostas)
    {
        var handler = new HandlerFalso(respostas);
        var http = new HttpClient(handler);
        var config = new ConfiguracaoMetaFalsa(tokenSistema);
        return (new GraphMetaClient(http, config, NullLogger<GraphMetaClient>.Instance), handler);
    }

    /// <summary>Devolve as respostas combinadas, na ordem, e guarda as URLs pedidas.</summary>
    private sealed class HandlerFalso((HttpStatusCode Codigo, string Corpo)[] respostas) : HttpMessageHandler
    {
        private int _proxima;

        public List<string> Chamadas { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Chamadas.Add(request.RequestUri!.ToString());

            var (codigo, corpo) = _proxima < respostas.Length
                ? respostas[_proxima++]
                : (HttpStatusCode.InternalServerError, """{"error":{"message":"resposta nao combinada no teste"}}""");

            return Task.FromResult(new HttpResponseMessage(codigo)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ConfiguracaoMetaFalsa(string? tokenSistema) : IConfiguracaoMetaService
    {
        public Task<CredenciaisMeta> ObterAsync(CancellationToken ct = default)
            => Task.FromResult(new CredenciaisMeta(
                "app-de-teste", "segredo", "verify", tokenSistema, "https://graph.facebook.com/v21.0/"));

        public Task<ConfiguracaoMeta> ObterBrutaAsync(CancellationToken ct = default)
            => Task.FromResult(new ConfiguracaoMeta());

        public Task SalvarAsync(AtualizarConfiguracaoMeta dados, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
