using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Integracoes.SisregWeb.Rede;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>
/// Endereço do SISREG mostrado na fonte "SISREG III" de Sistema → Fontes de dados (ADR-0077): IP
/// atual, desde quando, se sai pelo túnel e o histórico das trocas. A rota manteve o prefixo antigo. Só leitura — quem conserta a rota é o timer do servidor
/// (docs/sisreg-egress.md §Verificador).
/// </summary>
[ApiController]
[Route("integracoes/sisreg/endereco")]
public sealed class SisregEnderecoController(IEnderecoSisregService service) : ControllerBase
{
    [HttpGet]
    [RequerPermissao(ModuloPermissao.FontesDados, AcoesPermissao.Consulta)]
    [ProducesResponseType<EnderecoSisregDto>(StatusCodes.Status200OK)]
    public async Task<EnderecoSisregDto> Obter(CancellationToken cancellationToken) =>
        await service.ObterAsync(cancellationToken);
}
