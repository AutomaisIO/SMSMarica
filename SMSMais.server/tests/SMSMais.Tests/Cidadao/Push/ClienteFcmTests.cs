using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using SMSMais.Core.Cidadao.Push;

namespace SMSMais.Tests.Cidadao.Push;

/// <summary>
/// Cliente do FCM HTTP v1 sem rede: o Google é dublado por um <see cref="HttpMessageHandler"/> que
/// guarda o que foi enviado. Sob teste, o contrato com o Google (corpo exato da mensagem, asserção
/// JWT da conta de serviço e o form do token) e a leitura dos erros — é ela que decide se o token
/// do aparelho é apagado, e apagar por engano deixa o cidadão sem notificação até reabrir o app.
/// </summary>
public sealed class ClienteFcmTests
{
    private sealed class GoogleFalso(params (HttpStatusCode Status, string Corpo)[] respostas) : HttpMessageHandler
    {
        private int _i;
        public List<(HttpMethod Metodo, string Url, string? Autorizacao, string? TipoConteudo, string Corpo)> Requisicoes { get; } = [];
        public Exception? Lancar { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requisicoes.Add((
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct)));
            if (Lancar is not null) throw Lancar;
            var (status, corpo) = respostas[Math.Min(_i++, respostas.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }

    private static readonly (string Json, RSA Chave) Conta = ContaServicoFcmFabrica.Gerar();

    private static ContaServicoFcm ContaLida() => ContaServicoFcm.Ler(Conta.Json);

    private static ClienteFcm Cliente(GoogleFalso google, IMemoryCache? cache = null) =>
        new(new HttpClient(google), cache ?? new MemoryCache(new MemoryCacheOptions()));

    private static string ErroFcm(int codigo, string status, string? errorCode, string mensagem, string? campoViolado = null)
    {
        var detalhes = new JsonArray();
        if (errorCode is not null)
            detalhes.Add(new JsonObject
            {
                ["@type"] = "type.googleapis.com/google.firebase.fcm.v1.FcmError",
                ["errorCode"] = errorCode,
            });
        if (campoViolado is not null)
            detalhes.Add(new JsonObject
            {
                ["@type"] = "type.googleapis.com/google.rpc.BadRequest",
                ["fieldViolations"] = new JsonArray(new JsonObject
                {
                    ["field"] = campoViolado,
                    ["description"] = "Invalid registration token",
                }),
            });
        return new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["code"] = codigo,
                ["message"] = mensagem,
                ["status"] = status,
                ["details"] = detalhes,
            },
        }.ToJsonString();
    }

    private static MensagemFcm Mensagem(string? rota = "/exames") =>
        new("token-do-aparelho", "Seu exame foi agendado", "Abra o app para ver o dia e o local.",
            rota is null
                ? new Dictionary<string, string> { ["notificacaoId"] = "0199c0de-0000-7000-8000-000000000001" }
                : new Dictionary<string, string> { ["rota"] = rota, ["notificacaoId"] = "0199c0de-0000-7000-8000-000000000001" });

    // ---------- mensagem ----------

    [Fact]
    public async Task Envio_monta_o_corpo_exato_do_contrato_com_bearer_e_url_do_projeto()
    {
        var google = new GoogleFalso((HttpStatusCode.OK, "{\"name\":\"projects/projeto-teste/messages/1\"}"));

        var desfecho = await Cliente(google).EnviarAsync(ContaLida(), "ya29.acesso", Mensagem());

        Assert.True(desfecho.Entregue);
        Assert.False(desfecho.AparelhoRemovido);
        var (metodo, url, autorizacao, tipo, corpo) = Assert.Single(google.Requisicoes);
        Assert.Equal(HttpMethod.Post, metodo);
        Assert.Equal("https://fcm.googleapis.com/v1/projects/projeto-teste/messages:send", url);
        Assert.Equal("Bearer ya29.acesso", autorizacao);
        Assert.Equal("application/json", tipo);

        var esperado = JsonNode.Parse("""
            { "message": {
              "token": "token-do-aparelho",
              "notification": { "title": "Seu exame foi agendado", "body": "Abra o app para ver o dia e o local." },
              "data": { "rota": "/exames", "notificacaoId": "0199c0de-0000-7000-8000-000000000001" },
              "android": { "priority": "HIGH", "notification": { "channel_id": "avisos" } },
              "apns": { "headers": { "apns-priority": "10" }, "payload": { "aps": { "sound": "default" } } }
            } }
            """);
        Assert.True(JsonNode.DeepEquals(esperado, JsonNode.Parse(corpo)), corpo);
    }

    [Fact]
    public async Task Sem_rota_o_data_leva_so_o_id_da_notificacao()
    {
        var google = new GoogleFalso((HttpStatusCode.OK, "{}"));

        await Cliente(google).EnviarAsync(ContaLida(), "ya29.acesso", Mensagem(rota: null));

        var data = JsonNode.Parse(Assert.Single(google.Requisicoes).Corpo)!["message"]!["data"]!.AsObject();
        Assert.Equal(new[] { "notificacaoId" }, data.Select(kv => kv.Key));
        Assert.All(data, kv => Assert.Equal(JsonValueKind.String, kv.Value!.GetValueKind()));
    }

    // ---------- erros ----------

    [Fact]
    public async Task Unregistered_marca_o_aparelho_como_removido()
    {
        var google = new GoogleFalso((HttpStatusCode.NotFound,
            ErroFcm(404, "NOT_FOUND", "UNREGISTERED", "Requested entity was not found.")));

        var d = await Cliente(google).EnviarAsync(ContaLida(), "ya29.acesso", Mensagem());

        Assert.False(d.Entregue);
        Assert.True(d.AparelhoRemovido);
        Assert.Equal("O app foi desinstalado ou as notificações foram desligadas neste aparelho.", d.Detalhe);
        Assert.Equal(GravidadeFalhaFcm.Nenhuma, d.Gravidade);
    }

    [Fact]
    public void Token_invalido_tambem_remove_mas_payload_invalido_nao()
    {
        var tokenInvalido = InterpretadorRespostaFcm.Interpretar(HttpStatusCode.BadRequest,
            ErroFcm(400, "INVALID_ARGUMENT", "INVALID_ARGUMENT", "The registration token is not a valid FCM registration token", "message.token"));
        var payloadInvalido = InterpretadorRespostaFcm.Interpretar(HttpStatusCode.BadRequest,
            ErroFcm(400, "INVALID_ARGUMENT", "INVALID_ARGUMENT", "Invalid value at 'message.data[0].value'", "message.data[0].value"));

        Assert.True(tokenInvalido.AparelhoRemovido);
        Assert.False(payloadInvalido.AparelhoRemovido);
        Assert.Equal(GravidadeFalhaFcm.Erro, payloadInvalido.Gravidade);
        Assert.Contains("Invalid value at 'message.data[0].value'", payloadInvalido.Detalhe);
    }

    [Fact]
    public async Task Sender_id_mismatch_e_configuracao_nao_remove_o_aparelho_nem_o_access_token()
    {
        // App gerado com um projeto do Firebase, chave em Integrações de outro: o token do aparelho
        // está bom (para o projeto certo) e o access token também (para o projeto dele).
        var conta = ContaLida();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var google = new GoogleFalso(
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.acesso\",\"expires_in\":3599}"),
            (HttpStatusCode.Forbidden, ErroFcm(403, "PERMISSION_DENIED", "SENDER_ID_MISMATCH", "SenderId mismatch")));
        var cliente = Cliente(google, cache);

        var token = await cliente.ObterAccessTokenAsync(conta);
        var d = await cliente.EnviarAsync(conta, token, Mensagem());
        await cliente.ObterAccessTokenAsync(conta);

        Assert.False(d.Entregue);
        Assert.False(d.AparelhoRemovido);
        Assert.False(d.CredencialRecusada);
        Assert.Equal(GravidadeFalhaFcm.Erro, d.Gravidade);
        Assert.Equal("Este aparelho está ligado a outro projeto do Firebase. O app precisa ser gerado com o mesmo projeto da chave em Integrações → Firebase.", d.Detalhe);
        Assert.Equal("app ligado a outro projeto do Firebase", d.Resumo);
        Assert.Contains("SENDER_ID_MISMATCH", d.Tecnico);
        Assert.Equal(2, google.Requisicoes.Count);
    }

    [Fact]
    public async Task Third_party_auth_error_nao_remove_e_aponta_a_chave_da_apple()
    {
        var google = new GoogleFalso((HttpStatusCode.Unauthorized,
            ErroFcm(401, "UNAUTHENTICATED", "THIRD_PARTY_AUTH_ERROR", "Auth error from APNS or Web Push Service")));

        var d = await Cliente(google).EnviarAsync(ContaLida(), "ya29.acesso", Mensagem());

        Assert.False(d.Entregue);
        Assert.False(d.AparelhoRemovido);
        Assert.Equal(GravidadeFalhaFcm.Erro, d.Gravidade);
        Assert.Equal("O Firebase recusou a chave da Apple (APNs). Confira a configuração do projeto.", d.Detalhe);
        Assert.False(d.CredencialRecusada);
    }

    [Fact]
    public async Task Indisponivel_503_nao_remove_e_so_avisa()
    {
        var google = new GoogleFalso((HttpStatusCode.ServiceUnavailable,
            ErroFcm(503, "UNAVAILABLE", "UNAVAILABLE", "The service is currently unavailable.")));

        var d = await Cliente(google).EnviarAsync(ContaLida(), "ya29.acesso", Mensagem());

        Assert.False(d.Entregue);
        Assert.False(d.AparelhoRemovido);
        Assert.Equal(GravidadeFalhaFcm.Aviso, d.Gravidade);
        Assert.Equal("O Firebase não respondeu agora. Tente de novo em alguns minutos.", d.Detalhe);
    }

    [Fact]
    public async Task Falha_de_rede_vira_indisponivel_sem_lancar()
    {
        var google = new GoogleFalso { Lancar = new HttpRequestException("conexão recusada") };

        var d = await Cliente(google).EnviarAsync(ContaLida(), "ya29.acesso", Mensagem());

        Assert.False(d.Entregue);
        Assert.Equal(GravidadeFalhaFcm.Aviso, d.Gravidade);
    }

    [Fact]
    public async Task Credencial_recusada_no_envio_descarta_o_access_token_do_cache()
    {
        var conta = ContaLida();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var google = new GoogleFalso(
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.velho\",\"expires_in\":3599,\"token_type\":\"Bearer\"}"),
            (HttpStatusCode.Unauthorized, ErroFcm(401, "UNAUTHENTICATED", null, "Request had invalid authentication credentials.")),
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.novo\",\"expires_in\":3599,\"token_type\":\"Bearer\"}"));
        var cliente = Cliente(google, cache);

        var velho = await cliente.ObterAccessTokenAsync(conta);
        var d = await cliente.EnviarAsync(conta, velho, Mensagem());
        var novo = await cliente.ObterAccessTokenAsync(conta);

        Assert.True(d.CredencialRecusada);
        Assert.Equal(GravidadeFalhaFcm.Erro, d.Gravidade);
        Assert.Equal("ya29.novo", novo);
    }

    // ---------- envio de validação (Testar) ----------

    private static string ErroApiGoogle(int codigo, string status, string mensagem, string? razao = null)
    {
        var detalhes = new JsonArray();
        if (razao is not null)
            detalhes.Add(new JsonObject
            {
                ["@type"] = "type.googleapis.com/google.rpc.ErrorInfo",
                ["reason"] = razao,
                ["domain"] = "googleapis.com",
                ["metadata"] = new JsonObject { ["service"] = "fcm.googleapis.com", ["consumer"] = "projects/123456" },
            });
        return new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["code"] = codigo,
                ["message"] = mensagem,
                ["status"] = status,
                ["details"] = detalhes,
            },
        }.ToJsonString();
    }

    private const string MensagemApiDesligada =
        "Firebase Cloud Messaging API has not been used in project 123456 before or it is disabled. Enable it by visiting https://console.developers.google.com/apis/api/fcm.googleapis.com/overview?project=123456 then retry.";

    [Fact]
    public async Task Validacao_manda_validate_only_para_um_topico_e_200_e_credencial_valida()
    {
        var google = new GoogleFalso((HttpStatusCode.OK, "{\"name\":\"projects/projeto-teste/messages/fake_message_id\"}"));

        var v = await Cliente(google).ValidarEnvioAsync(ContaLida(), "ya29.acesso");

        Assert.True(v.Ok);
        Assert.Equal("Credencial válida: o servidor autenticou e o Firebase aceita envios do projeto projeto-teste.", v.Mensagem);
        var (metodo, url, autorizacao, tipo, corpo) = Assert.Single(google.Requisicoes);
        Assert.Equal(HttpMethod.Post, metodo);
        Assert.Equal("https://fcm.googleapis.com/v1/projects/projeto-teste/messages:send", url);
        Assert.Equal("Bearer ya29.acesso", autorizacao);
        Assert.Equal("application/json", tipo);

        // validate_only no topo (fora de "message") e destino tópico: nada chega a aparelho nenhum.
        var esperado = JsonNode.Parse("""
            { "validate_only": true,
              "message": {
                "topic": "teste-credencial",
                "notification": { "title": "Teste", "body": "Teste da credencial do servidor." }
            } }
            """);
        Assert.True(JsonNode.DeepEquals(esperado, JsonNode.Parse(corpo)), corpo);
        Assert.DoesNotContain("\"token\"", corpo);
    }

    [Fact]
    public async Task Validacao_com_api_desligada_manda_ativar_no_google_cloud_sem_mostrar_o_texto_do_google()
    {
        var google = new GoogleFalso((HttpStatusCode.Forbidden,
            ErroApiGoogle(403, "PERMISSION_DENIED", MensagemApiDesligada, "SERVICE_DISABLED")));

        var v = await Cliente(google).ValidarEnvioAsync(ContaLida(), "ya29.acesso");

        Assert.False(v.Ok);
        Assert.Equal("A API Firebase Cloud Messaging (V1) está desligada no projeto projeto-teste. Ative no Google Cloud (APIs e serviços) e teste de novo.", v.Mensagem);
        Assert.Contains("SERVICE_DISABLED", v.Tecnico);
    }

    [Fact]
    public void Validacao_reconhece_api_desligada_so_pelo_error_info()
    {
        // O texto do Google muda; o ErrorInfo é o sinal estável e precisa bastar sozinho.
        var v = InterpretadorRespostaFcm.InterpretarValidacao(HttpStatusCode.Forbidden,
            ErroApiGoogle(403, "PERMISSION_DENIED", "Mensagem reescrita pelo Google.", "SERVICE_DISABLED"), "projeto-teste");

        Assert.False(v.Ok);
        Assert.StartsWith("A API Firebase Cloud Messaging (V1) está desligada no projeto projeto-teste.", v.Mensagem);
    }

    [Theory]
    [InlineData(MensagemApiDesligada)]
    [InlineData("Cloud Messaging API is disabled for this project.")]
    public void Validacao_reconhece_api_desligada_pelo_texto_quando_nao_vem_o_error_info(string mensagem)
    {
        var v = InterpretadorRespostaFcm.InterpretarValidacao(HttpStatusCode.Forbidden,
            ErroApiGoogle(403, "PERMISSION_DENIED", mensagem), "projeto-teste");

        Assert.False(v.Ok);
        Assert.StartsWith("A API Firebase Cloud Messaging (V1) está desligada no projeto projeto-teste.", v.Mensagem);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "PERMISSION_DENIED", "The caller does not have permission", "IAM_PERMISSION_DENIED")]
    [InlineData(HttpStatusCode.Forbidden, "PERMISSION_DENIED", "Permission 'cloudmessaging.messages.create' denied", null)]
    [InlineData(HttpStatusCode.Unauthorized, "UNAUTHENTICATED", "Request had invalid authentication credentials.", null)]
    public async Task Validacao_403_ou_401_e_conta_sem_permissao_de_envio(
        HttpStatusCode status, string statusGoogle, string mensagem, string? razao)
    {
        var google = new GoogleFalso((status, ErroApiGoogle((int)status, statusGoogle, mensagem, razao)));

        var v = await Cliente(google).ValidarEnvioAsync(ContaLida(), "ya29.acesso");

        Assert.False(v.Ok);
        Assert.Equal("A conta de serviço não tem permissão de envio no projeto projeto-teste (papel Firebase Cloud Messaging Admin ou Firebase Admin SDK).", v.Mensagem);
        Assert.DoesNotContain(mensagem, v.Mensagem);
    }

    [Fact]
    public async Task Validacao_404_e_projeto_nao_encontrado()
    {
        var google = new GoogleFalso((HttpStatusCode.NotFound,
            ErroApiGoogle(404, "NOT_FOUND", "Requested entity was not found.")));

        var v = await Cliente(google).ValidarEnvioAsync(ContaLida(), "ya29.acesso");

        Assert.False(v.Ok);
        Assert.Equal("O Firebase não encontrou o projeto projeto-teste. Confira se o JSON é da conta de serviço do projeto certo e cole de novo.", v.Mensagem);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "O Firebase não respondeu agora (HTTP 503). Tente de novo em alguns minutos.")]
    [InlineData(HttpStatusCode.TooManyRequests, "O Firebase não respondeu agora (HTTP 429). Tente de novo em alguns minutos.")]
    [InlineData(HttpStatusCode.BadRequest, "O Firebase recusou o envio de teste do projeto projeto-teste (HTTP 400).")]
    public async Task Validacao_com_outro_status_e_generica_e_nao_vaza_o_corpo(HttpStatusCode status, string esperado)
    {
        var google = new GoogleFalso((status, "<html>corpo cru do balanceador com detalhe interno</html>"));

        var v = await Cliente(google).ValidarEnvioAsync(ContaLida(), "ya29.acesso");

        Assert.False(v.Ok);
        Assert.Equal(esperado, v.Mensagem);
        Assert.Contains("corpo cru", v.Tecnico);
    }

    [Fact]
    public async Task Validacao_sem_resposta_nao_lanca()
    {
        var rede = new GoogleFalso { Lancar = new HttpRequestException("conexão recusada") };
        var timeout = new GoogleFalso { Lancar = new TaskCanceledException("timeout do HttpClient") };

        var semRede = await Cliente(rede).ValidarEnvioAsync(ContaLida(), "ya29.acesso");
        var estourou = await Cliente(timeout).ValidarEnvioAsync(ContaLida(), "ya29.acesso");

        Assert.False(semRede.Ok);
        Assert.Equal("O Firebase não respondeu agora. Tente de novo em alguns minutos.", semRede.Mensagem);
        Assert.False(estourou.Ok);
        Assert.Equal("O Firebase não respondeu agora. Tente de novo em alguns minutos.", estourou.Mensagem);
    }

    [Fact]
    public async Task Validacao_cancelada_por_quem_pediu_propaga_e_nao_vira_indisponivel()
    {
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();
        var google = new GoogleFalso { Lancar = new TaskCanceledException("cancelado") };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Cliente(google).ValidarEnvioAsync(ContaLida(), "ya29.acesso", cancelado.Token));
    }

    [Fact]
    public async Task Validacao_401_descarta_o_access_token_do_cache()
    {
        var conta = ContaLida();
        var google = new GoogleFalso(
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.velho\",\"expires_in\":3599}"),
            (HttpStatusCode.Unauthorized, ErroApiGoogle(401, "UNAUTHENTICATED", "Request had invalid authentication credentials.")),
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.novo\",\"expires_in\":3599}"));
        var cliente = Cliente(google);

        var velho = await cliente.ObterAccessTokenAsync(conta);
        await cliente.ValidarEnvioAsync(conta, velho);

        Assert.Equal("ya29.novo", await cliente.ObterAccessTokenAsync(conta));
    }

    // ---------- access token da conta de serviço ----------

    [Fact]
    public async Task Access_token_sai_de_jwt_rs256_assinado_pela_chave_da_conta_e_fica_em_cache()
    {
        var google = new GoogleFalso((HttpStatusCode.OK,
            "{\"access_token\":\"ya29.acesso\",\"expires_in\":3599,\"token_type\":\"Bearer\"}"));
        var cliente = Cliente(google);
        var antes = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var token = await cliente.ObterAccessTokenAsync(ContaLida());
        var deNovo = await cliente.ObterAccessTokenAsync(ContaLida());

        Assert.Equal("ya29.acesso", token);
        Assert.Equal("ya29.acesso", deNovo);
        var (metodo, url, _, tipo, corpo) = Assert.Single(google.Requisicoes);
        Assert.Equal(HttpMethod.Post, metodo);
        Assert.Equal("https://oauth2.googleapis.com/token", url);
        Assert.Equal("application/x-www-form-urlencoded", tipo);

        var form = corpo.Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]));
        Assert.Equal(new[] { "grant_type", "assertion" }, form.Keys);
        Assert.Equal("urn:ietf:params:oauth:grant-type:jwt-bearer", form["grant_type"]);

        var partes = form["assertion"].Split('.');
        Assert.Equal(3, partes.Length);

        using var cabecalho = JsonDocument.Parse(Base64Url.DecodeFromChars(partes[0]));
        Assert.Equal("RS256", cabecalho.RootElement.GetProperty("alg").GetString());
        Assert.Equal("JWT", cabecalho.RootElement.GetProperty("typ").GetString());
        Assert.Equal(ContaServicoFcmFabrica.PrivateKeyId, cabecalho.RootElement.GetProperty("kid").GetString());

        using var declaracoes = JsonDocument.Parse(Base64Url.DecodeFromChars(partes[1]));
        var c = declaracoes.RootElement;
        Assert.Equal(ContaServicoFcmFabrica.ClientEmail, c.GetProperty("iss").GetString());
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", c.GetProperty("scope").GetString());
        Assert.Equal("https://oauth2.googleapis.com/token", c.GetProperty("aud").GetString());
        var iat = c.GetProperty("iat").GetInt64();
        Assert.InRange(iat, antes - 5, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 5);
        Assert.Equal(iat + 3600, c.GetProperty("exp").GetInt64());

        var assinaturaValida = Conta.Chave.VerifyData(
            Encoding.ASCII.GetBytes($"{partes[0]}.{partes[1]}"),
            Base64Url.DecodeFromChars(partes[2]),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        Assert.True(assinaturaValida);
    }

    [Fact]
    public async Task Renovar_ignora_o_cache()
    {
        var google = new GoogleFalso(
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.um\",\"expires_in\":3599}"),
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.dois\",\"expires_in\":3599}"));
        var cliente = Cliente(google);

        await cliente.ObterAccessTokenAsync(ContaLida());
        var renovado = await cliente.ObterAccessTokenAsync(ContaLida(), renovar: true);

        Assert.Equal("ya29.dois", renovado);
        Assert.Equal(2, google.Requisicoes.Count);
    }

    [Fact]
    public async Task Cache_e_por_conta_e_nao_guarda_token_que_vence_em_menos_de_5_minutos()
    {
        var google = new GoogleFalso(
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.conta-a\",\"expires_in\":3599}"),
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.conta-b\",\"expires_in\":3599}"),
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.curto-1\",\"expires_in\":300}"),
            (HttpStatusCode.OK, "{\"access_token\":\"ya29.curto-2\",\"expires_in\":300}"));
        var cliente = Cliente(google);
        // Trocar o JSON em Integrações (outra chave da mesma conta) não pode reaproveitar o token antigo.
        var outraChave = ContaServicoFcm.Ler(ContaServicoFcmFabrica.Gerar().Json);
        var terceira = ContaServicoFcm.Ler(ContaServicoFcmFabrica.Gerar().Json);

        Assert.Equal("ya29.conta-a", await cliente.ObterAccessTokenAsync(ContaLida()));
        Assert.Equal("ya29.conta-b", await cliente.ObterAccessTokenAsync(outraChave));
        Assert.Equal("ya29.conta-a", await cliente.ObterAccessTokenAsync(ContaLida()));
        Assert.Equal("ya29.curto-1", await cliente.ObterAccessTokenAsync(terceira));
        Assert.Equal("ya29.curto-2", await cliente.ObterAccessTokenAsync(terceira));
        Assert.Equal(4, google.Requisicoes.Count);
    }

    [Fact]
    public async Task Google_recusando_a_conta_lanca_com_a_mensagem_dele_e_desfecho_de_credencial()
    {
        var google = new GoogleFalso((HttpStatusCode.BadRequest,
            "{\"error\":\"invalid_grant\",\"error_description\":\"Invalid JWT Signature.\"}"));

        var ex = await Assert.ThrowsAsync<FalhaTokenFcmException>(() => Cliente(google).ObterAccessTokenAsync(ContaLida()));

        Assert.Equal("invalid_grant: Invalid JWT Signature.", ex.Message);
        Assert.True(ex.Desfecho.CredencialRecusada);
        Assert.Equal(GravidadeFalhaFcm.Erro, ex.Desfecho.Gravidade);
        Assert.False(ex.Desfecho.AparelhoRemovido);
    }

    [Fact]
    public async Task Servidor_de_token_fora_do_ar_e_indisponivel_nao_credencial()
    {
        var google = new GoogleFalso((HttpStatusCode.ServiceUnavailable, "{\"error\":\"backend_error\"}"));

        var ex = await Assert.ThrowsAsync<FalhaTokenFcmException>(() => Cliente(google).ObterAccessTokenAsync(ContaLida()));

        Assert.False(ex.Desfecho.CredencialRecusada);
        Assert.Equal(GravidadeFalhaFcm.Aviso, ex.Desfecho.Gravidade);
    }
}
