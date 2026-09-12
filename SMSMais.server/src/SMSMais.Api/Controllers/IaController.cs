using Microsoft.AspNetCore.Mvc;
using SMSMais.Api.Auth;
using SMSMais.Core.Identidade;
using SMSMais.Core.Inteligencia;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Core.Inteligencia.Fontes;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Api.Controllers;

/// <summary>Bases ativas do módulo IA (consumidas pela Consulta Inteligente e pela configuração).</summary>
[ApiController]
[Route("ia")]
public sealed class IaController(
    IIaService service,
    IIdentidadeService identidade,
    IUsuarioAtualAccessor usuarioAtual) : ControllerBase
{
    private readonly IIaService _service = service;

    /// <summary>
    /// Só as bases que o usuário pode usar: a base Atendimento some da lista de quem não tem o
    /// módulo próprio (a sessão e o proxy SQL recusam de novo — a lista é conveniência, não trava).
    /// </summary>
    [HttpGet("fontes")]
    [RequerPermissao(ModuloPermissao.Inteligencia, AcoesPermissao.Consulta)]
    [ProducesResponseType<IReadOnlyList<FonteResumoDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<FonteResumoDto>> ListarFontesAtivas(CancellationToken cancellationToken)
    {
        var fontes = await _service.ListarFontesAtivasAsync(cancellationToken);
        var visiveis = new List<FonteResumoDto>(fontes.Count);
        foreach (var f in fontes)
        {
            var tipo = Enum.TryParse<TipoFonte>(f.Tipo, out var t) ? t : (TipoFonte?)null;
            if (tipo is null
                || await PermissaoDaFonte.PodeUsarAsync(identidade, usuarioAtual.UsuarioId, tipo.Value, cancellationToken))
            {
                visiveis.Add(f);
            }
        }

        return visiveis;
    }
}
