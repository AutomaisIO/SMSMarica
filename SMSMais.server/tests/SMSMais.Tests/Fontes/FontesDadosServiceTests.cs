using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.EsusSg;
using SMSMais.Core.Fontes;
using SMSMais.Core.Fontes.Dtos;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.KlinikosWeb;
using SMSMais.Core.Integracoes.Sisreg;
using SMSMais.Core.Integracoes.Sisreg.Configuracao;
using SMSMais.Core.Integracoes.Sisreg.Dtos;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Inteligencia.Configuracao;
using SMSMais.Core.Inteligencia.Fontes;
using SMSMais.Core.Inteligencia.Fontes.Agente;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Core.Ser;
using SMSMais.Core.Sernit;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Fontes;

/// <summary>
/// Sistema → Fontes de dados (ADR-0076). O JSON da credencial e o da fonte Klinikos Web carregam a
/// conexão E o uso (agenda dos motores, cadência da importação): a tela de fontes só pode mexer na
/// conexão. E trocar o login do SER/SERNIT/ESUS SG continua passando pelo motor, que testa antes de
/// gravar.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class FontesDadosServiceTests(PostgresFixture fixture)
{
    private sealed record Cenario(
        FontesDadosService Fontes,
        IntegracaoCredencialService Credenciais,
        SisregConfiguracaoService SisregApi,
        ISerMotorService SerMotor,
        ISerWebSessao SerSessao,
        KlinikosWebUsoService KlinikosUso);

    private static Cenario Montar(SmsMaisDbContext db)
    {
        var protetor = Substitute.For<IProtetorSegredos>();
        protetor.Proteger(Arg.Any<string>()).Returns(c => "cifrado:" + c.Arg<string>());
        protetor.Revelar(Arg.Any<string>()).Returns(c => c.Arg<string>().Replace("cifrado:", ""));
        var usuario = new UsuarioAtualAccessorFake(Guid.NewGuid());

        var credenciais = new IntegracaoCredencialService(db, protetor, usuario);
        var sisregApi = new SisregConfiguracaoService(db, protetor, usuario);
        var bases = new IaFonteService(db, protetor, Substitute.For<IFonteDadosFactory>(),
            Substitute.For<IAgenteSqlRegistry>(), new ConfigurationBuilder().Build(), usuario);
        var serMotor = Substitute.For<ISerMotorService>();
        var serSessao = Substitute.For<ISerWebSessao>();

        var fontes = new FontesDadosService(db, bases, credenciais, sisregApi,
            Substitute.For<ISisregConsultaService>(), serMotor, Substitute.For<ISernitMotorService>(),
            Substitute.For<IEsusSgMotorService>(), serSessao, Substitute.For<ISernitWebSessao>(),
            Substitute.For<IEsusSgSessao>());

        return new Cenario(fontes, credenciais, sisregApi, serMotor, serSessao, new KlinikosWebUsoService(db, usuario));
    }

    private static AtualizarFonteDadosRequest Edicao(
        string? usuario = null, string? senha = null, string? baseUrl = null, bool ativo = true,
        Dictionary<string, string?>? parametros = null) =>
        new(null, null, null, null, null, null, usuario, senha, null, baseUrl, null, ativo, parametros);

    private static JsonObject Json(string? texto) => (JsonObject)JsonNode.Parse(texto!)!;

    private static async Task<string?> JsonDaCredencialAsync(SmsMaisDbContext db, string provedor) =>
        (await db.IntegracaoCredenciais.AsNoTracking().SingleAsync(c => c.Provedor == provedor)).ParametrosJson;

    [Fact]
    public async Task Trocar_login_do_SER_passa_pelo_motor_e_preserva_a_agenda()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        await c.Credenciais.AtualizarAsync(SerWebSessao.Provedor, new AtualizarIntegracaoCredencialRequest(
            "op", "senha", null,
            """{"baseUrl":"https://antigo","varreduraAtiva":true,"varreduraHoraLocal":"03:00","anexoTamanhoMaximoBytes":5000000}""",
            true));

        await c.Fontes.AtualizarAsync(SerWebSessao.Provedor, Edicao("novo.op", "nova", baseUrl: "https://novo"));

        await c.SerMotor.Received(1).SalvarCredencialAsync("novo.op", "nova", Arg.Any<CancellationToken>());
        var json = Json(await JsonDaCredencialAsync(db, SerWebSessao.Provedor));
        Assert.Equal("https://novo", (string?)json["baseUrl"]);
        Assert.True((bool)json["varreduraAtiva"]!);
        Assert.Equal("03:00", (string?)json["varreduraHoraLocal"]);
        Assert.Equal(5000000, (long)json["anexoTamanhoMaximoBytes"]!);
        // URL nova só vale num login novo.
        c.SerSessao.Received(1).Reiniciar();
    }

    [Fact]
    public async Task Login_do_SER_pela_metade_e_recusado_sem_chamar_o_motor()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
            c.Fontes.AtualizarAsync(SerWebSessao.Provedor, Edicao(usuario: "so.o.usuario")));

        Assert.Contains("juntos", ex.Message);
        await c.SerMotor.DidNotReceiveWithAnyArgs().SalvarCredencialAsync(default!, default!, default);
    }

    [Fact]
    public async Task Parametro_de_uso_pela_tela_de_fontes_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);

        await Assert.ThrowsAsync<ValidacaoException>(() => c.Fontes.AtualizarAsync(SerWebSessao.Provedor,
            Edicao(parametros: new() { ["varreduraAtiva"] = "false" })));
    }

    [Fact]
    public async Task Sisreg_web_troca_o_login_e_mantem_fila_escalas_e_indicadores()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        await c.Credenciais.AtualizarAsync("sisreg", new AtualizarIntegracaoCredencialRequest(
            "op", "senha", null,
            """{"baseUrl":"https://sisreg","autoLogin":false,"filaReleituraAtiva":true,"escalasAtivo":true,"indicadoresColetaPausadaAte":"2026-10-21T10:00:00Z"}""",
            true));

        await c.Fontes.AtualizarAsync("sisreg", Edicao("op2", "senha2", baseUrl: "https://sisreg",
            parametros: new() { ["autoLogin"] = "true" }));

        var json = Json(await JsonDaCredencialAsync(db, "sisreg"));
        Assert.True((bool)json["autoLogin"]!);
        Assert.True((bool)json["filaReleituraAtiva"]!);
        Assert.True((bool)json["escalasAtivo"]!);
        Assert.NotNull(json["indicadoresColetaPausadaAte"]);
        var ctx = await c.Credenciais.ObterContextoAsync("sisreg");
        Assert.Equal("op2", ctx.ClientId);
        Assert.Equal("senha2", ctx.ClientSecret);
    }

    [Fact]
    public async Task Lista_traz_os_seis_sistemas_e_nunca_o_login_dos_operadores()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        await c.Credenciais.AtualizarAsync("esuspec", new AtualizarIntegracaoCredencialRequest(
            "12345678901", "senha", null, null, true));

        var lista = await c.Fontes.ListarAsync();

        var sistemas = lista.Where(f => f.Origem == "sistema").Select(f => f.Chave).ToList();
        Assert.Equal(["sisreg", "sisreg-api", "ser", "sernit", "esussg", "esuspec"], sistemas);
        var pec = lista.Single(f => f.Chave == "esuspec");
        Assert.True(pec.LoginDefinido);
        Assert.Null(pec.Usuario);
        Assert.False(pec.Testavel);
    }

    [Fact]
    public async Task Klinikos_web_conexao_e_cadencia_nao_se_apagam_uma_a_outra()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        var slug = "klinikos-teste-" + Guid.NewGuid().ToString("N")[..8];
        var id = await c.Fontes.CriarAsync(new CriarFonteDadosRequest(
            "Klinikos teste", slug, "KlinikosWeb", null, "Producao", null, null, null, "user", "pwd",
            "https://klinikos.exemplo", null,
            new Dictionary<string, string?> { ["appRoot"] = "/UPA24H", ["unidCodigo"] = "0006", ["build"] = "U.2025.06.1.26" }));

        await c.KlinikosUso.SalvarAsync(id, new SalvarKlinikosWebUsoRequest(true, true, 30, 5));
        await c.Fontes.AtualizarAsync(id.ToString(), Edicao(parametros: new() { ["unidCodigo"] = "0007" }));

        var fonte = await db.IaFontes.AsNoTracking().SingleAsync(f => f.Id == id);
        Assert.Equal("klinikos", fonte.Familia);
        var json = Json(fonte.ParametrosJson);
        Assert.Equal("0007", (string?)json["unidCodigo"]);
        Assert.Equal("/UPA24H", (string?)json["appRoot"]);
        Assert.True((bool)json["periodicoLigado"]!);
        Assert.Equal(30, (int)json["periodicoIntervaloMin"]!);
        Assert.True((bool)json["webPrimaria"]!);

        var dto = await c.Fontes.ObterAsync(id.ToString());
        Assert.Equal("prontuario-web", dto.Categoria);
        Assert.False(dto.Parametros.ContainsKey("periodicoLigado"));
    }

    [Fact]
    public async Task Base_sql_server_nao_nasce_pela_tela_nasce_pelo_agente()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);

        var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Fontes.CriarAsync(new CriarFonteDadosRequest(
            "Banco", null, "SqlServer", null, "Producao", "h", 1433, "db", "u", "p", null, null, null)));

        Assert.Contains("agente", ex.Message);
    }

    [Fact]
    public async Task Api_do_sisreg_conexao_e_uso_sao_gravados_separados()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        await c.SisregApi.AtualizarAsync(new AtualizarSisregConfiguracaoRequest(
            EscopoSisreg.Municipal, "RJ", "3302700", "330001", FonteCadastroPaciente.Sisreg, 2));

        await c.Fontes.AtualizarAsync(SistemasFonte.SisregApi, Edicao("login.api", "senha.api",
            baseUrl: "https://sisreg-es.exemplo/", parametros: new() { ["tipoAutenticacao"] = "Basic" }));
        await c.SisregApi.AtualizarAsync(new AtualizarSisregConfiguracaoRequest(
            EscopoSisreg.Municipal, "RJ", "3302700", "330001,330002", FonteCadastroPaciente.Sisreg, 2));

        var cfg = await db.SisregConfiguracoes.AsNoTracking().SingleAsync();
        Assert.Equal("login.api", cfg.Login);
        Assert.Equal("https://sisreg-es.exemplo/", cfg.BaseUrl);
        Assert.Equal("330001,330002", cfg.CentraisReguladoras);
        Assert.Equal(2, cfg.ConsultasSimultaneasSer);
        Assert.NotNull(cfg.SenhaCifrada);
    }

    [Fact]
    public async Task Remover_um_sistema_limpa_o_login_e_mantem_a_agenda()
    {
        await using var db = fixture.CriarDbContext();
        var c = Montar(db);
        await c.Credenciais.AtualizarAsync(SernitWebSessao.Provedor, new AtualizarIntegracaoCredencialRequest(
            "op", "senha", null, """{"varreduraAtiva":true}""", true));

        await c.Fontes.RemoverAsync(SernitWebSessao.Provedor);

        var cred = await c.Credenciais.ObterAsync(SernitWebSessao.Provedor);
        Assert.False(cred.ClientIdDefinido);
        Assert.False(cred.Ativo);
        Assert.True((bool)Json(cred.ParametrosJson)["varreduraAtiva"]!);
    }

    [Fact]
    public void Mesclar_so_toca_nas_chaves_de_conexao()
    {
        var json = FontesDadosService.MesclarChaves(
            """{"baseUrl":"https://a","autoLogin":true,"escalasAtivo":true}""",
            new Dictionary<string, string?> { ["baseUrl"] = "", ["autoLogin"] = "false" },
            ["baseUrl", "autoLogin"]);

        var obj = Json(json);
        Assert.False(obj.ContainsKey("baseUrl")); // vazio = volta ao padrão do conector
        Assert.False((bool)obj["autoLogin"]!);
        Assert.True((bool)obj["escalasAtivo"]!);
    }
}
