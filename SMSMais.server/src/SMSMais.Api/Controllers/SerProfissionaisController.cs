using Microsoft.AspNetCore.Mvc;

using SMSMais.Api.Auth;
using SMSMais.Core.Ser.Background;
using SMSMais.Core.Ser.Profissionais;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// <b>Regulação → SER → Médicos</b>: o espelho dos profissionais do SER, à parte do nosso
/// cadastro de Médicos (ADR-0065).
///
/// <para><b>Nada aqui escreve no SER.</b> "Importar do SER" só lê a pesquisa de Cadastro →
/// Profissionais; ligar/desligar mexe só na nossa base. O envio de médico novo ao SER é decisão
/// separada, ainda desligada (ADR-0065 §Envio).</para>
/// </summary>
[ApiController]
[Route("regulacao/ser/profissionais")]
public sealed class SerProfissionaisController(ISerProfissionalService servico) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<PaginaSerProfissionaisDto>(StatusCodes.Status200OK)]
    public Task<PaginaSerProfissionaisDto> Listar(
        [FromQuery] SerProfissionaisFiltro filtro, CancellationToken cancellationToken) =>
        servico.ListarAsync(filtro, cancellationToken);

    [HttpGet("resumo")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Consulta)]
    [ProducesResponseType<SerProfissionaisResumoDto>(StatusCodes.Status200OK)]
    public Task<SerProfissionaisResumoDto> Resumo(CancellationToken cancellationToken) =>
        servico.ResumoAsync(cancellationToken);

    /// <summary>
    /// Enfileira a leitura da pesquisa inteira do SER (~47 páginas) e responde na hora; a tela
    /// acompanha pelo resumo. 409 quando já há uma em andamento.
    /// </summary>
    [HttpPost("importar")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Importar([FromServices] ISerProfissionalImportacaoFila fila) =>
        fila.TentarEnfileirar()
            ? Accepted()
            : Conflict(new ProblemDetails
            {
                Title = "Já existe uma importação em andamento.",
                Detail = "Aguarde terminar — a tela mostra quando acabar.",
                Status = StatusCodes.Status409Conflict,
            });

    /// <summary>Uma pessoa confirma que este profissional do SER é aquele médico nosso.</summary>
    [HttpPut("{id:guid}/medico")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerProfissionalDto>(StatusCodes.Status200OK)]
    public Task<SerProfissionalDto> Ligar(
        Guid id, [FromBody] LigarMedicoSerRequest req, CancellationToken cancellationToken) =>
        servico.LigarAsync(id, req, cancellationToken);

    [HttpDelete("{id:guid}/medico")]
    [RequerPermissao(ModuloPermissao.RegulacaoSer, AcoesPermissao.Edicao)]
    [ProducesResponseType<SerProfissionalDto>(StatusCodes.Status200OK)]
    public Task<SerProfissionalDto> Desligar(Guid id, CancellationToken cancellationToken) =>
        servico.DesligarAsync(id, cancellationToken);
}
