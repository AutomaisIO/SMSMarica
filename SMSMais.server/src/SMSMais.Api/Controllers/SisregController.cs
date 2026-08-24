using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.Sisreg;
using SMSMais.Core.Integracoes.Sisreg.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Feed de leitura do SISREG (Manual API SISREG v2.1 §4) — endpoints "com a nossa cara"
/// sobre as 6 consultas Elasticsearch. Só-leitura: o SISREG não aceita escrita por esta API.
/// Credenciais e centrais vêm da configuração (tela). Ver ADR-0012.
/// </summary>
[ApiController]
[Route("sisreg")]
public sealed class SisregController(ISisregConsultaService consulta) : ControllerBase
{
    private readonly ISisregConsultaService _consulta = consulta;

    /// <summary>§4.1 Novas solicitações ambulatoriais por intervalo de data de solicitação.</summary>
    [HttpGet("ambulatorial/novas-solicitacoes")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>>(StatusCodes.Status200OK)]
    public async Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> NovasSolicitacoes(
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        [FromQuery] int tamanho,
        CancellationToken cancellationToken) =>
        await _consulta.NovasSolicitacoesAmbulatoriaisAsync(
            new IntervaloDatas(inicio, fim), NormalizarTamanho(tamanho), cancellationToken);

    /// <summary>§4.2 Fila de solicitações ambulatoriais (pendentes/reenviadas).</summary>
    [HttpGet("ambulatorial/fila")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregBuscaResultado<SolicitacaoAmbulatorialSisregDto>>(StatusCodes.Status200OK)]
    public async Task<SisregBuscaResultado<SolicitacaoAmbulatorialSisregDto>> Fila(
        [FromQuery] int tamanho,
        CancellationToken cancellationToken) =>
        await _consulta.FilaSolicitacoesAmbulatoriaisAsync(NormalizarTamanho(tamanho), cancellationToken);

    /// <summary>§4.3 Solicitações agendadas por intervalo de data de aprovação.</summary>
    [HttpGet("ambulatorial/agendadas")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>>(StatusCodes.Status200OK)]
    public async Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> Agendadas(
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        [FromQuery] int tamanho,
        CancellationToken cancellationToken) =>
        await _consulta.SolicitacoesAgendadasAsync(
            new IntervaloDatas(inicio, fim), NormalizarTamanho(tamanho), cancellationToken);

    /// <summary>§4.4 Solicitações atendidas por intervalo de data de confirmação.</summary>
    [HttpGet("ambulatorial/atendidas")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>>(StatusCodes.Status200OK)]
    public async Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> Atendidas(
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        [FromQuery] int tamanho,
        CancellationToken cancellationToken) =>
        await _consulta.SolicitacoesAtendidasAsync(
            new IntervaloDatas(inicio, fim), NormalizarTamanho(tamanho), cancellationToken);

    /// <summary>§4.5 Solicitações canceladas/devolvidas pela CRAM.</summary>
    [HttpGet("ambulatorial/canceladas-devolvidas")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>>(StatusCodes.Status200OK)]
    public async Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> CanceladasDevolvidas(
        [FromQuery] int tamanho,
        CancellationToken cancellationToken) =>
        await _consulta.SolicitacoesCanceladasDevolvidasAsync(NormalizarTamanho(tamanho), cancellationToken);

    /// <summary>§4.6 Solicitações de internação (hospitalar) por intervalo de data de solicitação.</summary>
    [HttpGet("hospitalar/internacoes")]
    [RequerPermissao(ModuloPermissao.Sisreg, AcoesPermissao.Consulta)]
    [ProducesResponseType<SisregBuscaResultado<SolicitacaoHospitalarSisregDto>>(StatusCodes.Status200OK)]
    public async Task<SisregBuscaResultado<SolicitacaoHospitalarSisregDto>> Internacoes(
        [FromQuery] DateOnly inicio,
        [FromQuery] DateOnly fim,
        [FromQuery] int tamanho,
        CancellationToken cancellationToken) =>
        await _consulta.InternacoesHospitalaresAsync(
            new IntervaloDatas(inicio, fim), NormalizarTamanho(tamanho), cancellationToken);

    private static int NormalizarTamanho(int tamanho) => tamanho <= 0 ? 100 : tamanho;
}
