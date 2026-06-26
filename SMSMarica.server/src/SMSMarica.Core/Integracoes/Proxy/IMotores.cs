using SMSMarica.Core.Integracoes.Dtos;

namespace SMSMarica.Core.Integracoes.Proxy;

/// <summary>
/// Motor de consulta de CPF (validação na Receita). Cada implementação faz uma única
/// tentativa; timeout e retry são responsabilidade do orquestrador
/// (<see cref="IConsultaCpfService"/>). Sinaliza falha transitória com
/// <see cref="MotorIndisponivelException"/> (ou deixa <c>HttpRequestException</c>/parse
/// vazar) e negativa autoritativa com <see cref="MotorNaoEncontrouException"/>.
/// </summary>
public interface IMotorCpf
{
    /// <summary>Chave estável do motor (ver <see cref="MotoresProxy"/>).</summary>
    string Motor { get; }

    Task<HubCpfRespostaDto> ConsultarAsync(
        string cpf, DateOnly dataNascimento, MotorExecucao cfg, CancellationToken cancellationToken);
}

/// <summary>Motor de consulta de CEP. Mesmas convenções do <see cref="IMotorCpf"/>.</summary>
public interface IMotorCep
{
    string Motor { get; }

    Task<HubCepRespostaDto> ConsultarAsync(
        string cep, MotorExecucao cfg, CancellationToken cancellationToken);
}
