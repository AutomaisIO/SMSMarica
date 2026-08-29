using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Core.RoboAtendimento.Comandos;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ia;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.RoboAtendimento;

/// <summary>
/// Motor do robô sobre a Messages API (ADR-0050). O que está sob teste é a lição do incidente de
/// 26/08: a resposta ao cidadão só pode sair da ferramenta terminal, e a prosa de raciocínio do
/// modelo NUNCA pode virar mensagem. Também cobre o laço de tool-use, a contabilidade de tokens e
/// o comportamento em sobrecarga da API.
///
/// A API é dublada por um <see cref="HttpMessageHandler"/> — sem rede. Precisa de Postgres só
/// porque o motor lê a chave cifrada de <c>IaConfiguracao</c>.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RoboAtendimentoMotorApiTests(PostgresFixture fixture)
{
    private const string Modelo = "claude-haiku-4-5-20251001";

    // ---------- dublês ----------

    /// <summary>Devolve as respostas na ordem em que foram enfileiradas e guarda o que foi enviado.</summary>
    private sealed class ApiFalsa(params (HttpStatusCode Status, string Corpo)[] respostas) : HttpMessageHandler
    {
        private int _i;
        public List<string> Requisicoes { get; } = [];
        public int Chamadas => _i;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requisicoes.Add(await request.Content!.ReadAsStringAsync(ct));
            var (status, corpo) = respostas[Math.Min(_i++, respostas.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };
        }
    }

    private static string RespostaComTexto(string texto, bool handoff = false, double confianca = 0.9, int entrada = 1000, int saida = 50)
    {
        var t = JsonSerializer.Serialize(texto);
        var h = handoff ? "true" : "false";
        var c = confianca.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return "{\"stop_reason\":\"tool_use\",\"content\":[{\"type\":\"tool_use\",\"id\":\"tu_1\","
            + "\"name\":\"responder_cidadao\",\"input\":{\"texto\":" + t
            + ",\"handoff\":" + h + ",\"confianca\":" + c + "}}],"
            + "\"usage\":{\"input_tokens\":" + entrada + ",\"output_tokens\":" + saida + "}}";
    }

    private static string RespostaComComando(string comando, string argsJson) =>
        "{\"stop_reason\":\"tool_use\",\"content\":["
        + "{\"type\":\"text\",\"text\":\"vou verificar isso agora\"},"
        + "{\"type\":\"tool_use\",\"id\":\"tu_cmd\",\"name\":\"" + comando + "\",\"input\":" + argsJson + "}],"
        + "\"usage\":{\"input_tokens\":500,\"output_tokens\":20,\"cache_read_input_tokens\":200}}";

    /// <summary>Só prosa, sem ferramenta nenhuma — o caso que vazou jargão em produção.</summary>
    private const string RespostaSoProsa = """
        {"stop_reason":"end_turn","content":[
          {"type":"text","text":"Preciso carregar o schema da ferramenta verificar_cadastro para prosseguir."}
        ],"usage":{"input_tokens":300,"output_tokens":25}}
        """;

    private async Task<(RoboAtendimentoMotorApi Motor, IRoboComandoDispatcher Dispatcher, ApiFalsa Api)> CriarAsync(
        SmsMaisDbContext db, params (HttpStatusCode, string)[] respostas)
    {
        var protetor = Substitute.For<IProtetorSegredos>();
        protetor.Revelar(Arg.Any<string>()).Returns("sk-teste");
        if (!db.IaConfiguracoes.Any())
        {
            db.IaConfiguracoes.Add(new IaConfiguracao { Id = Guid.NewGuid(), Provedor = "anthropic", TokenCifrado = "cifrado", Modelo = Modelo });
            await db.SaveChangesAsync();
        }

        var api = new ApiFalsa(respostas);
        var http = new HttpClient(api) { BaseAddress = new Uri("https://api.anthropic.test/") };
        var dispatcher = Substitute.For<IRoboComandoDispatcher>();
        var motor = new RoboAtendimentoMotorApi(
            http, db, protetor, dispatcher, NullLogger<RoboAtendimentoMotorApi>.Instance);
        return (motor, dispatcher, api);
    }

    private static EntradaMotorRobo Entrada(params string[] comandos) => new(
        ChaveSessao: "t", ConversaId: Guid.NewGuid(), PacienteId: null, AssuntoId: Guid.NewGuid(),
        Modelo: Modelo, InstrucaoSistema: "Você é um atendente.", ComandosHabilitados: comandos,
        Historico: [new MensagemHistoricoRobo("cidadao", "oi"), new MensagemHistoricoRobo("robo", "olá!")],
        MensagemAtual: "cadê meu laudo?", DentroDoHorario: true);

    // ---------- a garantia central ----------

    [Fact]
    public async Task Resposta_sai_da_ferramenta_terminal_com_tokens_e_custo()
    {
        await using var db = fixture.CriarDbContext();
        var (motor, _, _) = await CriarAsync(db, (HttpStatusCode.OK, RespostaComTexto("Seu laudo sai em até 5 dias.")));

        var r = await motor.ResponderAsync(Entrada(), default);

        Assert.Equal("Seu laudo sai em até 5 dias.", r.Texto);
        Assert.False(r.HandOff);
        Assert.Equal(0.9, r.Confianca);
        Assert.Equal(1000, r.TokensEntrada);
        Assert.Equal(50, r.TokensSaida);
        // Haiku: (1000 × 1,00 + 50 × 5,00) / 1M
        Assert.Equal(0.00125m, r.CustoUsd);
    }

    [Fact]
    public async Task Modelo_que_so_escreve_prosa_NAO_fala_com_o_cidadao()
    {
        await using var db = fixture.CriarDbContext();
        var (motor, _, _) = await CriarAsync(db, (HttpStatusCode.OK, RespostaSoProsa));

        var r = await motor.ResponderAsync(Entrada(), default);

        // A regressão de 26/08: a prosa ("carregar o schema da ferramenta") chegou ao cidadão.
        Assert.DoesNotContain("schema", r.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ferramenta", r.Texto, StringComparison.OrdinalIgnoreCase);
        Assert.True(r.HandOff);
        Assert.Equal("sem-uso-da-ferramenta-de-saida", r.MotivoHandOff);
    }

    // ---------- laço de ferramentas ----------

    [Fact]
    public async Task Comando_pedido_pelo_modelo_executa_e_a_conversa_continua()
    {
        await using var db = fixture.CriarDbContext();
        var (motor, dispatcher, api) = await CriarAsync(db,
            (HttpStatusCode.OK, RespostaComComando("consultar_status_exame_recente", """{"cpf":"045"}""")),
            (HttpStatusCode.OK, RespostaComTexto("Seu exame já foi laudado.")));
        dispatcher.ExecutarAsync(default, default, default, default, default, default)
            .ReturnsForAnyArgs(new RoboComandoResultado(true, "Exame laudado em 20/08."));

        var r = await motor.ResponderAsync(Entrada(nameof(ComandoRobo.ConsultarStatusExameRecente)), default);

        Assert.Equal("Seu exame já foi laudado.", r.Texto);
        await dispatcher.Received(1).ExecutarAsync(
            Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(),
            ComandoRobo.ConsultarStatusExameRecente, Arg.Any<JsonElement>(), Arg.Any<CancellationToken>());
        // 2ª requisição leva o tool_result de volta ao modelo.
        Assert.Contains("tool_result", api.Requisicoes[1]);
        Assert.Contains("Exame laudado", api.Requisicoes[1]);
        // Tokens somam os dois turnos (incluindo leitura de cache).
        Assert.Equal(500 + 200 + 1000, r.TokensEntrada);
    }

    [Fact]
    public async Task So_ferramentas_habilitadas_no_assunto_sao_oferecidas()
    {
        await using var db = fixture.CriarDbContext();
        var (motor, _, api) = await CriarAsync(db, (HttpStatusCode.OK, RespostaComTexto("ok")));

        await motor.ResponderAsync(Entrada(nameof(ComandoRobo.InformarHorarioAtendimento)), default);

        // Conferir no ARRAY de tools, não no payload cru: o guardrail cita nomes de ferramentas
        // (justamente para proibir o modelo de falar deles), então buscar no texto daria falso.
        using var doc = JsonDocument.Parse(api.Requisicoes[0]);
        var tools = doc.RootElement.GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToList();

        Assert.Contains("informar_horario_atendimento", tools);
        Assert.Contains("responder_cidadao", tools);            // terminal, sempre presente
        Assert.DoesNotContain("confirmar_presenca", tools);     // não habilitada = não existe na sessão
        Assert.Equal(2, tools.Count);
        Assert.Contains("cache_control", api.Requisicoes[0]);   // prompt caching ligado

        // tool_choice OBRIGATÓRIO: sem ele o modelo responde em texto puro e o cidadão fica sem
        // resposta (o fallback seguro dispara). Foi o que a primeira simulação em produção pegou.
        Assert.Equal("any", doc.RootElement.GetProperty("tool_choice").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Laco_de_ferramenta_para_no_teto_e_devolve_mensagem_segura()
    {
        await using var db = fixture.CriarDbContext();
        // A API sempre pede comando de novo: sem teto, o laço seria infinito (e caro).
        var (motor, dispatcher, api) = await CriarAsync(db,
            (HttpStatusCode.OK, RespostaComComando("informar_horario_atendimento", "{}")));
        dispatcher.ExecutarAsync(default, default, default, default, default, default)
            .ReturnsForAnyArgs(new RoboComandoResultado(true, "8h às 17h."));

        var r = await motor.ResponderAsync(Entrada(nameof(ComandoRobo.InformarHorarioAtendimento)), default);

        Assert.True(r.HandOff);
        Assert.InRange(api.Chamadas, 2, 6);
    }

    // ---------- resiliência ----------

    [Fact]
    public async Task Sobrecarga_da_api_e_reesperada_no_proprio_turno()
    {
        await using var db = fixture.CriarDbContext();
        var (motor, _, api) = await CriarAsync(db,
            ((HttpStatusCode)529, """{"error":"overloaded"}"""),
            (HttpStatusCode.OK, RespostaComTexto("Tudo certo!")));

        var r = await motor.ResponderAsync(Entrada(), default);

        Assert.Equal("Tudo certo!", r.Texto);
        Assert.Equal(2, api.Chamadas); // reesperou em vez de devolver ao backoff de 4 min do worker
    }

    [Fact]
    public async Task Erro_permanente_sobe_para_o_worker_reagendar()
    {
        await using var db = fixture.CriarDbContext();
        var (motor, _, _) = await CriarAsync(db, (HttpStatusCode.BadRequest, """{"error":"modelo inválido"}"""));

        await Assert.ThrowsAsync<InvalidOperationException>(() => motor.ResponderAsync(Entrada(), default));
    }

    [Fact]
    public async Task Sem_token_configurado_o_motor_falha_explicito()
    {
        await using var db = fixture.CriarDbContext();
        foreach (var c in db.IaConfiguracoes) db.IaConfiguracoes.Remove(c);
        await db.SaveChangesAsync();

        var api = new ApiFalsa((HttpStatusCode.OK, RespostaComTexto("nunca chega aqui")));
        var motor = new RoboAtendimentoMotorApi(
            new HttpClient(api) { BaseAddress = new Uri("https://api.anthropic.test/") },
            db, Substitute.For<IProtetorSegredos>(), Substitute.For<IRoboComandoDispatcher>(),
            NullLogger<RoboAtendimentoMotorApi>.Instance);

        // Falha explícita (o worker reagenda) — nunca silêncio, nunca resposta inventada.
        await Assert.ThrowsAsync<InvalidOperationException>(() => motor.ResponderAsync(Entrada(), default));
        Assert.Equal(0, api.Chamadas);
    }

    // ---------- histórico ----------

    [Fact]
    public async Task Historico_vira_turnos_user_assistant_de_verdade()
    {
        await using var db = fixture.CriarDbContext();
        var (motor, _, api) = await CriarAsync(db, (HttpStatusCode.OK, RespostaComTexto("ok")));

        await motor.ResponderAsync(Entrada(), default);

        using var doc = JsonDocument.Parse(api.Requisicoes[0]);
        var msgs = doc.RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal("user", msgs[0].GetProperty("role").GetString());       // conversa começa no cidadão
        Assert.Equal("assistant", msgs[1].GetProperty("role").GetString());  // resposta anterior do robô
        Assert.Equal("user", msgs[^1].GetProperty("role").GetString());      // termina na mensagem atual
        Assert.Contains("cadê meu laudo?", msgs[^1].GetProperty("content").GetString());
    }
}
