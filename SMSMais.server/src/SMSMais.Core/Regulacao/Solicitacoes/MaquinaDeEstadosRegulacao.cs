using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Solicitacoes;

/// <summary>
/// Quais mudanças de estado existem, quem pode fazer cada uma, e que evento cada uma gera
/// (ADR-0052, plano 04).
///
/// <para><b>É pura e estática de propósito.</b> Sem banco, sem serviço, sem
/// <c>CancellationToken</c>: a tabela de transições é a regra do negócio destilada, e regra
/// destilada se testa em milissegundos e se lê de uma vez. As pré-condições que dependem de dado
/// (tem CPF? o formulário está completo? a credencial está disponível?) ficam no service — elas
/// não são "esta transição existe?", são "esta transição pode acontecer agora?".</para>
///
/// <para><b>Por que uma tabela e não <c>if</c>s espalhados:</b> o caminho da solicitação tem 12
/// estados e três atores. Com a regra espalhada, cada endpoint novo repete um pedaço dela, e é
/// questão de tempo até um deles deixar a ponta cancelar um caso que o agente já assumiu — o tipo
/// de defeito que só aparece quando alguém reclama.</para>
/// </summary>
public static class MaquinaDeEstadosRegulacao
{
    /// <param name="De">Estado atual.</param>
    /// <param name="Para">Estado pretendido.</param>
    /// <param name="Papel">Quem está agindo.</param>
    /// <param name="Evento">O que a trilha registra.</param>
    private sealed record Transicao(
        StatusRegulacao De, StatusRegulacao Para, PapelEventoRegulacao Papel, TipoEventoRegulacao Evento);

    private static readonly Transicao[] Permitidas =
    [
        // --- lado da unidade solicitante ---
        new(StatusRegulacao.Rascunho, StatusRegulacao.PendenteRegulacao,
            PapelEventoRegulacao.Solicitante, TipoEventoRegulacao.EnvioFila),
        new(StatusRegulacao.Devolvida, StatusRegulacao.PendenteRegulacao,
            PapelEventoRegulacao.Solicitante, TipoEventoRegulacao.EnvioFila),

        // Cancelar só antes de o agente assumir: depois disso, a ponta não puxa o tapete de quem
        // já está trabalhando no caso.
        new(StatusRegulacao.Rascunho, StatusRegulacao.Cancelada,
            PapelEventoRegulacao.Solicitante, TipoEventoRegulacao.Cancelamento),
        new(StatusRegulacao.PendenteRegulacao, StatusRegulacao.Cancelada,
            PapelEventoRegulacao.Solicitante, TipoEventoRegulacao.Cancelamento),

        // --- lado do agente regulador ---
        new(StatusRegulacao.PendenteRegulacao, StatusRegulacao.EmAnalise,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.Assumida),
        new(StatusRegulacao.EmAnalise, StatusRegulacao.Devolvida,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.Devolucao),
        new(StatusRegulacao.EmAnalise, StatusRegulacao.Recusada,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.Recusa),
        new(StatusRegulacao.Devolvida, StatusRegulacao.Recusada,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.Recusa),
        new(StatusRegulacao.EmAnalise, StatusRegulacao.EnviandoAoSistema,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.EnvioSistema),

        // Envio assistido (incremento 3): o agente inclui pela tela do sistema e digita o número.
        // Salta `EnviandoAoSistema` porque não há envio nosso em curso para travar.
        new(StatusRegulacao.EmAnalise, StatusRegulacao.EnviadaAoSistema,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.NumeroExterno),

        // D-8: a interna já nasceu no SISREG pelo solicitante; o "OK" do agente é ação local.
        new(StatusRegulacao.PendenteRegulacao, StatusRegulacao.EmFilaExterna,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.OkInterno),

        // Retentativa depois de uma falha — só o agente, e só depois de conferir duplicidade.
        new(StatusRegulacao.FalhaEnvio, StatusRegulacao.EnviandoAoSistema,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.EnvioSistema),
        new(StatusRegulacao.FalhaEnvio, StatusRegulacao.EmAnalise,
            PapelEventoRegulacao.Agente, TipoEventoRegulacao.Ajuste),

        // --- sistema (envio, varreduras, importação) ---
        new(StatusRegulacao.EnviandoAoSistema, StatusRegulacao.EnviadaAoSistema,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.NumeroExterno),
        new(StatusRegulacao.EnviandoAoSistema, StatusRegulacao.FalhaEnvio,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.FalhaEnvio),
        new(StatusRegulacao.EnviadaAoSistema, StatusRegulacao.EmFilaExterna,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.SituacaoExterna),
        new(StatusRegulacao.EmFilaExterna, StatusRegulacao.Agendada,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.SituacaoExterna),
        new(StatusRegulacao.Agendada, StatusRegulacao.EmFilaExterna,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.SituacaoExterna),
        new(StatusRegulacao.Agendada, StatusRegulacao.Concluida,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.SituacaoExterna),
        new(StatusRegulacao.EmFilaExterna, StatusRegulacao.Concluida,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.SituacaoExterna),
        new(StatusRegulacao.EmFilaExterna, StatusRegulacao.Cancelada,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.SituacaoExterna),
        new(StatusRegulacao.Agendada, StatusRegulacao.Cancelada,
            PapelEventoRegulacao.Sistema, TipoEventoRegulacao.SituacaoExterna),
    ];

    /// <summary>Estados a partir dos quais o caso já não anda mais.</summary>
    public static bool EhTerminal(StatusRegulacao status) =>
        status is StatusRegulacao.Concluida or StatusRegulacao.Cancelada or StatusRegulacao.Recusada;

    public static bool PodeTransitar(StatusRegulacao de, StatusRegulacao para, PapelEventoRegulacao papel) =>
        Permitidas.Any(t => t.De == de && t.Para == para && t.Papel == papel);

    /// <summary>
    /// Evento correspondente à transição. <c>null</c> quando ela não existe — quem chama trata
    /// isso como recusa, e não como "grava um evento genérico".
    /// </summary>
    public static TipoEventoRegulacao? EventoDe(
        StatusRegulacao de, StatusRegulacao para, PapelEventoRegulacao papel) =>
        Permitidas.FirstOrDefault(t => t.De == de && t.Para == para && t.Papel == papel)?.Evento;

    /// <summary>Para onde este ator consegue levar o caso a partir daqui — a tela usa para decidir botões.</summary>
    public static IReadOnlyList<StatusRegulacao> DestinosDe(StatusRegulacao de, PapelEventoRegulacao papel) =>
        [.. Permitidas.Where(t => t.De == de && t.Papel == papel).Select(t => t.Para).Distinct()];
}
