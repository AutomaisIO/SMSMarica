using System.Net.Http.Json;
using SMSMais.Core.Integracoes.Dtos;

namespace SMSMais.Core.Integracoes.Proxy.Motores;

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
            // status:false do Hub NÃO é sempre negativa: sem saldo/instabilidade/token também
            // chegam assim. Negativa autoritativa só quando a mensagem diz que os DADOS não
            // conferem; o resto vira indisponível (o executor retenta e cai pro fallback) —
            // com o motivo cru do Hub na exceção, que o executor loga (era descartado antes).
            var motivoCru = $"{payload?.Return} {payload?.Message}".Trim();
            if (payload is not null && HubDoDesenvolvedor.EhNegativaAutoritativa(payload.Return, payload.Message))
            {
                // Não vazamos a mensagem crua do Hub ao usuário (ex.: "NOK").
                throw new MotorNaoEncontrouException(
                    Motor, "CPF não foi validado pela Receita. Confira o CPF e a data de nascimento.");
            }
            throw new MotorIndisponivelException(
                Motor, motivoCru.Length > 0 ? $"status=false do Hub: {motivoCru}" : "payload vazio/ilegível");
        }

        return new HubCpfRespostaDto(
            Cpf: payload.Result.NumeroDeCpf ?? cpf,
            Nome: payload.Result.NomeDaPf ?? string.Empty,
            DataNascimento: payload.Result.DataNascimento ?? data,
            SituacaoCadastral: payload.Result.SituacaoCadastral,
            Sexo: NormalizarSexo(payload.Result.Genero ?? payload.Result.Sexo));
    }

    /// <summary>Normaliza o sexo cru do Hub (M/F/Masculino/Feminino) para o canônico do domínio.</summary>
    private static string? NormalizarSexo(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;
        return bruto.Trim().ToUpperInvariant() switch
        {
            "M" or "MASCULINO" => "Masculino",
            "F" or "FEMININO" => "Feminino",
            _ => null,
        };
    }
}
