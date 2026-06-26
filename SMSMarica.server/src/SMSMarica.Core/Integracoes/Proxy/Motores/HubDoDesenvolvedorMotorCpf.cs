using System.Net.Http.Json;
using SMSMarica.Core.Integracoes.Dtos;

namespace SMSMarica.Core.Integracoes.Proxy.Motores;

/// <summary>
/// Motor de CPF via Hub do Desenvolvedor (v2). Token/baseUrl vêm da configuração do motor.
/// Timeout/retry são aplicados pelo orquestrador (o <paramref name="cancellationToken"/> já
/// chega com o timeout da tentativa).
/// </summary>
public sealed class HubDoDesenvolvedorMotorCpf(HttpClient http) : IMotorCpf
{
    public string Motor => MotoresProxy.HubDoDesenvolvedor;

    public async Task<HubCpfRespostaDto> ConsultarAsync(
        string cpf, DateOnly dataNascimento, MotorExecucao cfg, CancellationToken cancellationToken)
    {
        var data = dataNascimento.ToString("dd/MM/yyyy");
        var url = $"{HubDoDesenvolvedor.BaseUrl(cfg)}cpf/?cpf={cpf}&data={data}&token={HubDoDesenvolvedor.Token(cfg)}";

        using var resposta = await http.GetAsync(url, cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            throw new MotorIndisponivelException(Motor, $"HTTP {(int)resposta.StatusCode}");
        }

        var payload = await resposta.Content
            .ReadFromJsonAsync<HubDoDesenvolvedor.CpfPayload>(HubDoDesenvolvedor.JsonOpts, cancellationToken);

        if (payload is null || !payload.Status || payload.Result is null)
        {
            // Negativa autoritativa: não vazamos a mensagem crua do Hub (ex.: "NOK").
            throw new MotorNaoEncontrouException(
                Motor,
                "CPF não foi validado pela Receita. Confira CPF e data de nascimento, ou tente novamente em instantes.");
        }

        return new HubCpfRespostaDto(
            Cpf: payload.Result.NumeroDeCpf ?? cpf,
            Nome: payload.Result.NomeDaPf ?? string.Empty,
            DataNascimento: payload.Result.DataNascimento ?? data,
            SituacaoCadastral: payload.Result.SituacaoCadastral);
    }
}
