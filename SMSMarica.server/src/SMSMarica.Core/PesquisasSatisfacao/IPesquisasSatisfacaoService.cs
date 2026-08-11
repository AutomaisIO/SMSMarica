using SMSMarica.Core.PesquisasSatisfacao.Dtos;

namespace SMSMarica.Core.PesquisasSatisfacao;

public interface IPesquisasSatisfacaoService
{
    /// <summary>
    /// Janela para responder, contada do fim do atendimento. Espelha <c>JANELA_DIAS</c> na tela
    /// do app — e é aqui que a regra vale de verdade: o que a tela esconde, um link guardado
    /// ainda alcançaria.
    /// </summary>
    static int JanelaDias => 15;

    /// <summary>Versão do instrumento em vigor. Muda quando a redação de qualquer pergunta muda.</summary>
    static string InstrumentoVersaoAtual => "pnass-2015-emergencia-v1";

    /// <summary>Contexto da pesquisa pelo token público (link do WhatsApp). Sem autenticação.</summary>
    Task<PesquisaPublicaDto> ObterPorTokenAsync(Guid token, CancellationToken cancellationToken = default);

    /// <summary>Grava as respostas do link público. Recusa fora da janela ou já respondida.</summary>
    Task ResponderPorTokenAsync(
        Guid token, IReadOnlyDictionary<string, string> respostas, string? ip,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava as respostas de quem entrou pelo app, já autenticado. Cria a pesquisa se ainda não
    /// existir — pelo histórico o cidadão pode avaliar sem nunca ter recebido convite.
    /// </summary>
    Task ResponderPeloAppAsync(
        Guid pacienteId, Guid encounterId, IReadOnlyDictionary<string, string> respostas, string? ip,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepara (ou recupera) a pesquisa de um atendimento e devolve a URL do convite. É o que a
    /// retaguarda usa no envio manual — idempotente por atendimento, para reenvio não render
    /// duas notas da mesma passagem.
    /// </summary>
    Task<EnvioPesquisaDto> PrepararEnvioAsync(
        Guid pacienteId, Guid encounterId, CancellationToken cancellationToken = default);
}
