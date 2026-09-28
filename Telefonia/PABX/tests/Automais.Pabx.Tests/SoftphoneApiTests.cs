using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Automais.Pabx.Api.Data.Entities;
using Automais.Pabx.Api.Ramais;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Automais.Pabx.Tests;

/// <summary>
/// Sobe a API inteira sobre uma pasta temporária no lugar de /etc/asterisk e do TFTP,
/// com AMI desligado: exercita criação, faixa, credencial e o arquivo gerado.
/// </summary>
public sealed class SoftphoneApiTests(SoftphoneApiTests.Fabrica fabrica) : IClassFixture<SoftphoneApiTests.Fabrica>
{
    private const string Chave = "chave-de-teste";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public sealed class Fabrica : WebApplicationFactory<Program>
    {
        public string Raiz { get; } = Path.Combine(Path.GetTempPath(), "pabx-testes-" + Guid.NewGuid().ToString("N"));

        public string SipConf => Path.Combine(Raiz, "etc", "sip_smsmarica.conf");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(Path.Combine(Raiz, "etc"));
            // Legado da FalarMais com um ramal que a API não pode recriar.
            File.WriteAllText(Path.Combine(Raiz, "etc", "sip_custom.conf"), "[6000]\nsecret=legado\ntype=friend\n");

            builder.UseSetting("Pabx:DbPath", Path.Combine(Raiz, "pabx.db"));
            builder.UseSetting("Pabx:KeysDir", Path.Combine(Raiz, "keys"));
            builder.UseSetting("Pabx:ApiKey", Chave);
            builder.UseSetting("Asterisk:SipConfPath", SipConf);
            builder.UseSetting("Asterisk:SipCustomConfPath", Path.Combine(Raiz, "etc", "sip_custom.conf"));
            builder.UseSetting("Asterisk:TftpDir", Path.Combine(Raiz, "tftp"));
            builder.UseSetting("Asterisk:BackupDir", Path.Combine(Raiz, "backups"));
            builder.UseSetting("Asterisk:Ami:Enabled", "false");
        }
    }

    private HttpClient Cliente(bool comChave = true)
    {
        var http = fabrica.CreateClient();
        if (comChave)
            http.DefaultRequestHeaders.Add("X-Api-Key", Chave);
        return http;
    }

    private static CriarRamalRequest Softphone(string numero) => new(
        numero, UnidadeId: 0, Descricao: "teste", Mac: null, Marca: null, Modelo: null,
        CallerId: "Atendente Teste", Tipo: TipoRamal.Softphone, DonoSistema: "smsmais", DonoId: "42");

    [Fact]
    public async Task Cria_softphone_gera_bloco_wss_e_entrega_credencial()
    {
        var http = Cliente();

        var criado = await http.PostAsJsonAsync("/api/ramais", Softphone("6001"), Json);
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);

        var conf = await File.ReadAllTextAsync(fabrica.SipConf);
        Assert.Contains("[6001]", conf);
        Assert.Contains("transport=wss", conf);

        var resposta = await http.GetAsync("/api/ramais/6001/credencial");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("no-store", resposta.Headers.CacheControl?.ToString());

        var credencial = await resposta.Content.ReadFromJsonAsync<CredencialSipDto>(Json);
        Assert.NotNull(credencial);
        Assert.Equal("6001", credencial.UsuarioSip);
        Assert.Equal(20, credencial.Senha.Length);
        Assert.Contains($"secret={credencial.Senha}", conf);
        Assert.StartsWith("wss://", credencial.WssUrl);

        var doDono = await http.GetFromJsonAsync<List<RamalDto>>("/api/ramais?donoSistema=smsmais&donoId=42", Json);
        Assert.Contains(doDono!, r => r.Numero == "6001");
    }

    [Fact]
    public async Task Credencial_exige_a_chave_de_servico()
    {
        await Cliente().PostAsJsonAsync("/api/ramais", Softphone("6002"), Json);

        var resposta = await Cliente(comChave: false).GetAsync("/api/ramais/6002/credencial");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Softphone_fora_da_faixa_e_recusado()
    {
        var resposta = await Cliente().PostAsJsonAsync("/api/ramais", Softphone("3050"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Numero_que_existe_no_legado_e_recusado_e_nao_e_sugerido()
    {
        var http = Cliente();

        var resposta = await http.PostAsJsonAsync("/api/ramais", Softphone("6000"), Json);
        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);

        var livres = await http.GetFromJsonAsync<FaixaLivreDto>("/api/ramais/faixas/livres?tipo=Softphone&quantidade=5", Json);
        Assert.DoesNotContain("6000", livres!.Sugestoes);
    }

    [Fact]
    public async Task CallerId_com_quebra_de_linha_nao_injeta_secao()
    {
        var pedido = Softphone("6003") with { CallerId = "x\n[9999]" };

        var resposta = await Cliente().PostAsJsonAsync("/api/ramais", pedido, Json);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Config_por_ramal_muda_call_limit_e_valida_allowlist()
    {
        var http = Cliente();
        await http.PostAsJsonAsync("/api/ramais", Softphone("6004"), Json);

        var ok = await http.PutAsJsonAsync("/api/ramais/6004/config",
            new AtualizarConfigRamalRequest("PLANO_HOSPITAIS", ["alaw"], 2), Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var conf = await File.ReadAllTextAsync(fabrica.SipConf);
        var bloco = conf[conf.IndexOf("[6004]", StringComparison.Ordinal)..];
        Assert.Contains("call-limit=2", bloco);

        var contextoProibido = await http.PutAsJsonAsync("/api/ramais/6004/config",
            new AtualizarConfigRamalRequest("from-internal", ["alaw"], 1), Json);
        Assert.Equal(HttpStatusCode.BadRequest, contextoProibido.StatusCode);
    }
}
