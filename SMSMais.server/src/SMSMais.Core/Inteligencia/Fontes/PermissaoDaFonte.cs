using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Inteligencia.Fontes;

/// <summary>
/// Quem pode perguntar a uma base, além de ter a Consulta Inteligente. Hoje só a base
/// <see cref="TipoFonte.Atendimento"/> tem trava própria: é conversa de cidadão, da rede toda.
/// </summary>
public static class PermissaoDaFonte
{
    /// <summary>Módulo exigido para usar a base; <c>null</c> = basta <see cref="ModuloPermissao.Inteligencia"/>.</summary>
    public static ModuloPermissao? ModuloExigido(TipoFonte tipo) => tipo switch
    {
        TipoFonte.Atendimento => ModuloPermissao.InteligenciaAtendimento,
        _ => null,
    };

    /// <summary>
    /// Bases do próprio SMSMais com dado de cidadão da rede toda: toda pergunta e todo SQL ficam
    /// em <c>ia_consulta</c>, e sem operador identificado a consulta não roda.
    /// </summary>
    public static bool AuditoriaObrigatoria(TipoFonte tipo) =>
        tipo is TipoFonte.Regulacao or TipoFonte.Atendimento;

    /// <summary>
    /// O usuário pode usar a base? Sem módulo exigido, sim. Com módulo, só usuário identificado
    /// que tenha <c>Consulta</c> nele — fail-closed: usuário desconhecido não passa.
    /// </summary>
    public static async Task<bool> PodeUsarAsync(
        IIdentidadeService identidade, Guid? usuarioId, TipoFonte tipo, CancellationToken ct)
    {
        if (ModuloExigido(tipo) is not { } modulo)
        {
            return true;
        }

        if (usuarioId is not { } id)
        {
            return false;
        }

        try
        {
            var permissoes = await identidade.ObterPermissoesResolvidasAsync(id, ct);
            return permissoes.Resolvidas.Any(p =>
                p.Modulo == modulo && (p.Acoes & AcoesPermissao.Consulta) == AcoesPermissao.Consulta);
        }
        catch (NaoEncontradoException)
        {
            return false;
        }
    }
}
