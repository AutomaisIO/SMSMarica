using System.Net.Http.Json;
using SMSMarica.Core.Integracoes.Dtos;

namespace SMSMarica.Core.Integracoes.Proxy.Motores;

/// <summary>Motor de CEP via Hub do Desenvolvedor (v2 — endpoint <c>cep3</c>).</summary>
public sealed class HubDoDesenvolvedorMotorCep(HttpClient http) : IMotorCep
{
    public string Motor => MotoresProxy.HubDoDesenvolvedor;

    public async Task<HubCepRespostaDto> ConsultarAsync(string cep, MotorExecucao cfg, CancellationToken cancellationToken)
    {
        var url = $"{HubDoDesenvolvedor.BaseUrl(cfg)}cep3/?cep={cep}&token={HubDoDesenvolvedor.Token(cfg)}";

        using var resposta = await http.GetAsync(url, cancellationToken);
        if (!resposta.IsSuccessStatusCode)
        {
            throw new MotorIndisponivelException(Motor, $"HTTP {(int)resposta.StatusCode}");
        }

        var payload = await resposta.Content
            .ReadFromJsonAsync<HubDoDesenvolvedor.CepPayload>(HubDoDesenvolvedor.JsonOpts, cancellationToken);
        var result = payload?.Result;

        if (payload is null || !payload.Status || result is null || string.IsNullOrWhiteSpace(result.Localidade))
        {
            // Mesma regra do motor de CPF: status:false só é negativa quando o Hub diz que o
            // CEP não existe; problema operacional (saldo/instabilidade/token) é indisponível
            // (retenta/fallback). Resposta com result preenchido-mas-incompleto = negativa.
            var temResultado = result is not null && payload!.Status;
            var motivoCru = $"{payload?.Return} {payload?.Message}".Trim();
            if (temResultado || (payload is not null && HubDoDesenvolvedor.EhNegativaAutoritativa(payload.Return, payload.Message)))
            {
                throw new MotorNaoEncontrouException(Motor, $"CEP {cep} não encontrado.");
            }
            throw new MotorIndisponivelException(
                Motor, motivoCru.Length > 0 ? $"status=false do Hub: {motivoCru}" : "payload vazio/ilegível");
        }

        return new HubCepRespostaDto(
            Cep: result.Cep ?? cep,
            Logradouro: result.Logradouro ?? string.Empty,
            Complemento: result.Complemento,
            Bairro: result.Bairro ?? string.Empty,
            Localidade: result.Localidade,
            Uf: result.Uf ?? string.Empty,
            Ibge: result.Ibge);
    }
}
