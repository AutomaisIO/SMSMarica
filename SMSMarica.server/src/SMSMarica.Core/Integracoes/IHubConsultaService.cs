using SMSMarica.Core.Integracoes.Dtos;

namespace SMSMarica.Core.Integracoes;

/// <summary>
/// Proxy para a API "Hub do Desenvolvedor". Token e BaseUrl ficam em
/// configuração (<c>Integracoes:HubDoDesenvolvedor</c>) — nunca no front.
/// </summary>
public interface IHubConsultaService
{
    /// <summary>Consulta CPF na Receita exigindo data de nascimento.</summary>
    Task<HubCpfRespostaDto> ConsultarCpfAsync(
        string cpf,
        DateOnly dataNascimento,
        CancellationToken cancellationToken = default);

    /// <summary>Consulta endereço por CEP.</summary>
    Task<HubCepRespostaDto> ConsultarCepAsync(
        string cep,
        CancellationToken cancellationToken = default);
}
