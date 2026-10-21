using System.Text.Json.Nodes;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Regulacao.EnvioSer;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Regulacao.EnvioSer;

/// <summary>
/// O tamanho máximo por arquivo de cada destino, configurado na aba dele e guardado no
/// <c>ParametrosJson</c> da credencial — sem apagar o que já mora lá (baseUrl, varredura automática).
/// </summary>
public class RegrasAnexoDestinoTests
{
    /// <summary>Credenciais em memória: só o que a regra usa (ParametrosJson, RedirectUri, Ativo).</summary>
    private sealed class CredenciaisEmMemoria : IIntegracaoCredencialService
    {
        public readonly Dictionary<string, string?> Parametros = [];

        public Task<IReadOnlyList<IntegracaoCredencialDto>> ListarAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IntegracaoCredencialDto>>([]);

        public Task<IntegracaoCredencialDto> ObterAsync(string provedor, CancellationToken cancellationToken = default) =>
            Parametros.TryGetValue(provedor, out var json)
                ? Task.FromResult(new IntegracaoCredencialDto(provedor, provedor, true, true, null, json, true))
                : throw new ValidacaoException("credencial.nao_configurada", "Credencial não configurada.");

        public Task AtualizarAsync(
            string provedor, AtualizarIntegracaoCredencialRequest request, CancellationToken cancellationToken = default)
        {
            Parametros[provedor] = request.ParametrosJson;
            return Task.CompletedTask;
        }

        public Task LimparAsync(string provedor, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IntegracaoCredencialContexto> ObterContextoAsync(
            string provedor, CancellationToken cancellationToken = default) =>
            Parametros.TryGetValue(provedor, out var json)
                ? Task.FromResult(new IntegracaoCredencialContexto(provedor, "id", "segredo", null, json, true))
                : throw new ValidacaoException("credencial.nao_configurada", "Credencial não configurada.");
    }

    [Fact]
    public async Task Sem_configuracao_vale_o_padrao_de_4_5_MB()
    {
        var regras = new RegrasAnexoDestinoService(new CredenciaisEmMemoria());

        var dto = await regras.ObterAsync(SistemaRegulacao.Ser, CancellationToken.None);

        dto.TamanhoMaximoMb.Should().Be(4.5m);
        dto.Configurado.Should().BeFalse();
        (await regras.TamanhoMaximoBytesAsync(SistemaRegulacao.Ser, CancellationToken.None))
            .Should().Be(AnexosParaSer.TamanhoMaximoPadrao);
    }

    [Fact]
    public async Task Cada_destino_tem_o_seu_limite_e_o_resto_do_json_fica()
    {
        var credenciais = new CredenciaisEmMemoria();
        credenciais.Parametros["ser"] = """{"baseUrl":"https://ser.exemplo","varreduraAtiva":true}""";
        credenciais.Parametros["sernit"] = """{"varreduraHoraLocal":"03:00"}""";
        var regras = new RegrasAnexoDestinoService(credenciais);

        var salvo = await regras.SalvarAsync(SistemaRegulacao.Sernit, 3.0m, CancellationToken.None);

        salvo.Should().Be(new RegrasAnexoDestinoDto(3.0m, Configurado: true));
        (await regras.TamanhoMaximoBytesAsync(SistemaRegulacao.Sernit, CancellationToken.None)).Should().Be(3 * 1024 * 1024);
        (await regras.TamanhoMaximoBytesAsync(SistemaRegulacao.Ser, CancellationToken.None))
            .Should().Be(AnexosParaSer.TamanhoMaximoPadrao, "mexer no SERNIT não muda o SER");

        var json = JsonNode.Parse(credenciais.Parametros["sernit"]!)!.AsObject();
        json["varreduraHoraLocal"]!.GetValue<string>().Should().Be("03:00", "a hora da varredura mora no mesmo JSON");
        credenciais.Parametros["ser"].Should().Contain("baseUrl");
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(60)]
    public async Task Fora_da_faixa_e_recusado(double mb)
    {
        var credenciais = new CredenciaisEmMemoria();
        credenciais.Parametros["ser"] = "{}";
        var regras = new RegrasAnexoDestinoService(credenciais);

        var acao = () => regras.SalvarAsync(SistemaRegulacao.Ser, (decimal)mb, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>().WithMessage("*entre 0,5 e 50 MB*");
    }

    [Fact]
    public async Task Destino_que_nao_recebe_anexo_e_recusado()
    {
        var regras = new RegrasAnexoDestinoService(new CredenciaisEmMemoria());

        var acao = () => regras.SalvarAsync(SistemaRegulacao.Sisreg, 3m, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>().WithMessage("*Só o SER e o SERNIT*");
    }

    [Fact]
    public async Task Sem_credencial_cadastrada_salvar_explica_o_que_falta()
    {
        var regras = new RegrasAnexoDestinoService(new CredenciaisEmMemoria());

        var acao = () => regras.SalvarAsync(SistemaRegulacao.Sernit, 3m, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidacaoException>().WithMessage("*credencial do SERNIT*");
    }

    [Fact]
    public async Task Valor_estragado_no_json_vale_o_padrao()
    {
        var credenciais = new CredenciaisEmMemoria();
        credenciais.Parametros["ser"] = $$"""{"{{RegrasAnexoDestinoService.ChaveTamanho}}":"abc"}""";
        var regras = new RegrasAnexoDestinoService(credenciais);

        (await regras.TamanhoMaximoBytesAsync(SistemaRegulacao.Ser, CancellationToken.None))
            .Should().Be(AnexosParaSer.TamanhoMaximoPadrao);
    }
}
