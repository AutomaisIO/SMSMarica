using Microsoft.AspNetCore.Mvc;
using SMSMarica.Api.Auth;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Sisreg;
using SMSMais.Core.Integracoes.Sisreg.Configuracao;
using SMSMais.Core.Integracoes.Sisreg.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Api.Controllers;

/// <summary>
/// Tela de configuração da integração SISREG: credenciais (cifradas, write-only),
/// UF/município, centrais reguladoras e esquema de autenticação. Ver ADR-0012.
/// </summary>
[ApiController]
[Route("sisreg/configuracao")]
public sealed class SisregConfiguracaoController(
    ISisregConfiguracaoService configuracaoService,
    ISisregConsultaService consultaService) : ControllerBase
{
    private readonly ISisregConfiguracaoService _configuracaoService = configuracaoService;
    private readonly ISisregConsultaService _consultaService = consultaService;

    [HttpGet]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregConfiguracaoDto>(StatusCodes.Status200OK)]
    public async Task<SisregConfiguracaoDto> Obter(CancellationToken cancellationToken) =>
        await _configuracaoService.ObterAsync(cancellationToken);

    [HttpPut]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Atualizar(
        [FromBody] AtualizarSisregConfiguracaoRequest request,
        CancellationToken cancellationToken)
    {
        await _configuracaoService.AtualizarAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Testa as credenciais com uma consulta mínima à fila. Não lança — devolve sucesso/erro.</summary>
    [HttpPost("testar-conexao")]
    [RequerPermissao(ModuloPermissao.SisregConfiguracao, AcoesPermissao.Edicao)]
    [ProducesResponseType<TestarConexaoSisregResultado>(StatusCodes.Status200OK)]
    public async Task<TestarConexaoSisregResultado> TestarConexao(CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _consultaService.FilaSolicitacoesAmbulatoriaisAsync(1, cancellationToken);
            return new TestarConexaoSisregResultado(true, $"Conexão OK. {resultado.Total} registro(s) na fila.");
        }
        catch (ValidacaoException ex)
        {
            return new TestarConexaoSisregResultado(false, ex.Message);
        }
        catch (ConflitoException ex)
        {
            return new TestarConexaoSisregResultado(false, ex.Message);
        }
    }
}
