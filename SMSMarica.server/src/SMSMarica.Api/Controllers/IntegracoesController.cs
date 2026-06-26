using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMarica.Core.Integracoes.Dtos;
using SMSMarica.Core.Integracoes.Proxy;
using SMSMarica.Core.Integracoes.Proxy.Configuracao;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Proxy para integrações externas de consulta (CPF na Receita, CEP). Cada serviço é
/// atendido por um ou mais <b>motores</b> (ex.: Hub do Desenvolvedor) tentados em cadeia de
/// fallback — token/timeout/tentativas por motor ficam no servidor (tela de Integrações).
///
/// <para>
/// <b>Autorização:</b> as consultas (<c>cpf</c>/<c>cep</c>) exigem JWT (filtro global) mas
/// não um módulo específico — são utilitários horizontais dos formulários (Pacientes,
/// Médicos, Motoristas, Usuários, ...). Já a configuração dos motores exige
/// <see cref="ModuloPermissao.IntegracoesConfig"/>.
/// </para>
/// </summary>
[ApiController]
[Route("integracoes")]
public sealed class IntegracoesController(
    IConsultaCpfService consultaCpf,
    IConsultaCepService consultaCep,
    IProxyMotorConfiguracaoService motores) : ControllerBase
{
    /// <summary>Consulta CPF na Receita exigindo data de nascimento.</summary>
    [HttpGet("cpf")]
    [ProducesResponseType<HubCpfRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<HubCpfRespostaDto> ConsultarCpf(
        [FromQuery] string cpf,
        [FromQuery] DateOnly dataNascimento,
        CancellationToken cancellationToken) =>
        await consultaCpf.ConsultarCpfAsync(cpf, dataNascimento, cancellationToken);

    /// <summary>Consulta endereço por CEP.</summary>
    [HttpGet("cep/{cep}")]
    [ProducesResponseType<HubCepRespostaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<HubCepRespostaDto> ConsultarCep(
        string cep,
        CancellationToken cancellationToken) =>
        await consultaCep.ConsultarCepAsync(cep, cancellationToken);

    // ---- Configuração dos motores de proxy (RBAC IntegracoesConfig) ----

    /// <summary>Lista os motores de um serviço (<c>cpf</c>/<c>cep</c>) com flags e parâmetros.</summary>
    [HttpGet("proxy/{servico}")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<ProxyMotorDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IReadOnlyList<ProxyMotorDto>> ListarMotores(
        string servico, CancellationToken cancellationToken) =>
        await motores.ListarAsync(servico, cancellationToken);

    /// <summary>Upsert de um motor: token (write-only), ativo, ordem, timeout e tentativas.</summary>
    [HttpPut("proxy/{servico}/{motor}")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AtualizarMotor(
        string servico,
        string motor,
        [FromBody] AtualizarProxyMotorRequest request,
        CancellationToken cancellationToken)
    {
        await motores.AtualizarAsync(servico, motor, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Testa um motor de CPF isoladamente (sem fallback), com a config salva dele. Sempre 200 —
    /// o sucesso/erro vem no corpo, com o dado retornado quando deu certo.
    /// </summary>
    [HttpPost("proxy/cpf/{motor}/testar")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType<ProxyTesteCpfResultado>(StatusCodes.Status200OK)]
    public async Task<ProxyTesteCpfResultado> TestarMotorCpf(
        string motor,
        [FromBody] TestarCpfRequest request,
        CancellationToken cancellationToken) =>
        await consultaCpf.TestarMotorAsync(motor, request.Cpf, request.DataNascimento, cancellationToken);

    /// <summary>Testa um motor de CEP isoladamente (sem fallback), com a config salva dele.</summary>
    [HttpPost("proxy/cep/{motor}/testar")]
    [RequerPermissao(ModuloPermissao.IntegracoesConfig, AcoesPermissao.Edicao)]
    [ProducesResponseType<ProxyTesteCepResultado>(StatusCodes.Status200OK)]
    public async Task<ProxyTesteCepResultado> TestarMotorCep(
        string motor,
        [FromBody] TestarCepRequest request,
        CancellationToken cancellationToken) =>
        await consultaCep.TestarMotorAsync(motor, request.Cep, cancellationToken);
}

/// <summary>Corpo do teste manual de um motor de CPF.</summary>
public sealed record TestarCpfRequest(string Cpf, DateOnly DataNascimento);

/// <summary>Corpo do teste manual de um motor de CEP.</summary>
public sealed record TestarCepRequest(string Cep);
