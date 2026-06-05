using SMSMarica.Core.Inteligencia.Dtos;

namespace SMSMarica.Core.Inteligencia;

/// <summary>
/// Orquestrador do módulo IA: recebe a pergunta + bases-alvo, recupera conhecimento, gera SQL
/// via provedor de IA, executa read-only, corrige em caso de erro (gerando aprendizado auto) e
/// devolve a resposta abstraída (resumo + dados + visualização) por base.
/// </summary>
public interface IIaService
{
    Task<PerguntarRespostaDto> PerguntarAsync(PerguntarRequest request, CancellationToken cancellationToken = default);

    /// <summary>Bases ativas para popular o dropdown de seleção na tela de perguntar.</summary>
    Task<IReadOnlyList<FonteResumoDto>> ListarFontesAtivasAsync(CancellationToken cancellationToken = default);
}
