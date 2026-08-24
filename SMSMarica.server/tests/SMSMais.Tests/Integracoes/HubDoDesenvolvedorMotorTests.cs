using System.Net;
using System.Text;
using SMSMais.Core.Integracoes.Proxy;
using SMSMais.Core.Integracoes.Proxy.Motores;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Classificação das respostas <c>status:false</c> do Hub do Desenvolvedor: NEGATIVA
/// autoritativa (dados divergem/não encontrados → não retenta) vs INDISPONIBILIDADE do
/// fornecedor (sem saldo/instável/token → retenta e cai pro fallback). Antes, qualquer
/// status:false virava "CPF não foi validado pela Receita" — inclusive com o Hub fora do ar.
/// </summary>
public class HubDoDesenvolvedorMotorTests
{
    private sealed class HandlerFixo(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var resp = new HttpResponseMessage(status);
            if (status == HttpStatusCode.OK)
                resp.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return Task.FromResult(resp);
        }
    }

    private static readonly MotorExecucao Cfg = new("token-teste", 10, 1, null);

    private static HubDoDesenvolvedorMotorCpf MotorCpf(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new HandlerFixo(json, status)));

    private static HubDoDesenvolvedorMotorCep MotorCep(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new HandlerFixo(json, status)));

    private static Task<SMSMais.Core.Integracoes.Dtos.HubCpfRespostaDto> ConsultarCpf(HubDoDesenvolvedorMotorCpf m) =>
        m.ConsultarAsync("03622090731", new DateOnly(1962, 7, 16), Cfg, CancellationToken.None);

    // ---- CPF ----

    [Theory]
    [InlineData("""{"status":false,"return":"Dados divergentes"}""")]
    [InlineData("""{"status":false,"message":"CPF ou data de nascimento não conferem"}""")]
    [InlineData("""{"status":false,"return":"NOK","message":"CPF não encontrado na base"}""")]
    public async Task Cpf_status_false_com_dados_divergentes_e_negativa(string json)
    {
        await Assert.ThrowsAsync<MotorNaoEncontrouException>(() => ConsultarCpf(MotorCpf(json)));
    }

    [Theory]
    [InlineData("""{"status":false,"message":"Limite de consultas atingido, aguarde"}""")]
    [InlineData("""{"status":false,"return":"Token inválido ou bloqueado"}""")]
    [InlineData("""{"status":false,"message":"Consulta indisponível no momento, tente novamente"}""")]
    [InlineData("""{"status":false}""")] // motivo desconhecido → nunca negar cadastro por falha do fornecedor
    public async Task Cpf_status_false_operacional_e_indisponivel_para_retentar(string json)
    {
        await Assert.ThrowsAsync<MotorIndisponivelException>(() => ConsultarCpf(MotorCpf(json)));
    }

    [Fact]
    public async Task Cpf_http_500_e_indisponivel()
    {
        await Assert.ThrowsAsync<MotorIndisponivelException>(
            () => ConsultarCpf(MotorCpf("", HttpStatusCode.InternalServerError)));
    }

    [Fact]
    public async Task Cpf_sucesso_mapeia_dto_e_normaliza_sexo()
    {
        const string json = """
        {"status":true,"return":"OK","result":{
            "numero_de_cpf":"036.220.907-31","nome_da_pf":"FULANA DE TAL",
            "data_nascimento":"16/07/1962","situacao_cadastral":"REGULAR","genero":"F"}}
        """;
        var dto = await ConsultarCpf(MotorCpf(json));

        Assert.Equal("FULANA DE TAL", dto.Nome);
        Assert.Equal("Feminino", dto.Sexo);
        Assert.Equal("REGULAR", dto.SituacaoCadastral);
    }

    // ---- CEP ----

    [Fact]
    public async Task Cep_status_false_operacional_e_indisponivel()
    {
        const string json = """{"status":false,"message":"Sem saldo para realizar a consulta"}""";
        await Assert.ThrowsAsync<MotorIndisponivelException>(
            () => MotorCep(json).ConsultarAsync("24900000", Cfg, CancellationToken.None));
    }

    [Fact]
    public async Task Cep_nao_encontrado_e_negativa()
    {
        const string json = """{"status":false,"return":"CEP não encontrado"}""";
        await Assert.ThrowsAsync<MotorNaoEncontrouException>(
            () => MotorCep(json).ConsultarAsync("99999999", Cfg, CancellationToken.None));
    }

    [Fact]
    public async Task Cep_resultado_sem_localidade_e_negativa()
    {
        // status:true com result incompleto = resposta autoritativa vazia, não instabilidade.
        const string json = """{"status":true,"return":"OK","result":{"cep":"24900-000","localidade":""}}""";
        await Assert.ThrowsAsync<MotorNaoEncontrouException>(
            () => MotorCep(json).ConsultarAsync("24900000", Cfg, CancellationToken.None));
    }

    [Fact]
    public async Task Cep_sucesso_mapeia_dto()
    {
        const string json = """
        {"status":true,"return":"OK","result":{
            "cep":"24900-000","logradouro":"Rua A","bairro":"Centro","localidade":"Maricá","uf":"RJ","ibge":"3302700"}}
        """;
        var dto = await MotorCep(json).ConsultarAsync("24900000", Cfg, CancellationToken.None);

        Assert.Equal("Maricá", dto.Localidade);
        Assert.Equal("RJ", dto.Uf);
    }
}
