using SMSMais.Core.PesquisasSatisfacao.Dtos;

namespace SMSMais.Core.PesquisasSatisfacao;

public interface IPesquisasSatisfacaoService
{
    /// <summary>
    /// Janela para responder, contada do fim do atendimento. Espelha <c>JANELA_DIAS</c> na tela
    /// do app — e é aqui que a regra vale de verdade: o que a tela esconde, um link guardado
    /// ainda alcançaria.
    /// </summary>
    static int JanelaDias => 15;

    /// <summary>
    /// Prepara (ou recupera) a pesquisa de um atendimento e devolve a URL do convite. É o que a
    /// retaguarda usa no envio manual — idempotente por atendimento, para reenvio não render
    /// duas notas da mesma passagem.
    /// </summary>
    Task<EnvioPesquisaDto> PrepararEnvioAsync(
        Guid pacienteId, Guid encounterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepara e DISPARA o convite por WhatsApp. Envio manual, feito da tela do histórico —
    /// é como o fluxo será validado antes de existir gatilho automático.
    /// </summary>
    Task<EnvioPesquisaDto> EnviarAsync(
        Guid pacienteId, Guid encounterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conta o clique e devolve o destino — o link da AvanteSocial da unidade do atendimento.
    ///
    /// <para>O destino sai <b>limpo</b>: nenhum identificador nosso viaja junto. É essa ausência
    /// que sustenta o anonimato — eles ficam com a resposta sem saber de quem, nós com o clique
    /// sem saber o que foi respondido, e as duas metades nunca se encontram.</para>
    /// </summary>
    Task<string> RegistrarCliqueAsync(Guid token, CancellationToken cancellationToken = default);

    /// <summary>Configuração da pesquisa de uma unidade (aba da tela da unidade).</summary>
    Task<PesquisaConfigDto> ObterConfigAsync(Guid unidadeId, CancellationToken cancellationToken = default);

    /// <summary>Salva a configuração. Recusa ligar o disparo sem link de resposta.</summary>
    Task<PesquisaConfigDto> SalvarConfigAsync(
        Guid unidadeId, SalvarPesquisaConfigRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Varre os atendimentos encerrados e dispara os convites vencidos, unidade a unidade.
    /// Chamado pelo motor de fundo; devolve quantos saíram.
    /// </summary>
    Task<int> ProcessarGatilhoAsync(CancellationToken cancellationToken = default);

    /// <summary>Painel da unidade: enviadas, entregues, vistas, clicadas e perfil de quem clicou.</summary>
    Task<PesquisaPainelDto> ObterPainelAsync(
        Guid unidadeId, int dias, CancellationToken cancellationToken = default);
}
