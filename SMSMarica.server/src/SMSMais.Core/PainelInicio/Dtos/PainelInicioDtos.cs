using SMSMais.Core.SolicitacoesExame.Dtos;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.PainelInicio.Dtos;

/// <summary>
/// A lente de escopo do painel. <c>Unidade</c> = o que a(s) unidade(s) do usuário enxerga(m);
/// <c>Municipio</c> = a rede inteira, sem escopo.
///
/// É assim que a REGULAÇÃO é modelada: ela não é executante nem solicitante, e deliberadamente
/// não é uma <c>Unidade</c> — o ADR-0029 já rejeitou a unidade fantasma "Central de Regulação",
/// que entraria em todo select de executante/solicitante e em faturamento fingindo dado
/// assistencial. Regulação é a AUSÊNCIA de escopo. Ver ADR-0033 §5.
/// </summary>
public enum LenteEscopoPainel
{
    Unidade = 1,
    Municipio = 2,
}

/// <summary>
/// Recorte executante × solicitante dentro da lente de unidade — o pedido literal do operador
/// ("saber o que foi confirmado para ela como executante, e o que foi confirmado como solicitante").
/// Ignorado na lente Município, onde não há "minha" unidade de referência.
/// </summary>
public enum DirecaoPainel
{
    Tudo = 0,
    Executante = 1,
    Solicitante = 2,
}

/// <summary>
/// Uma raia do painel: a contagem total mais as primeiras linhas. A raia inteira é <c>null</c> na
/// resposta quando o usuário não tem a permissão da tela que ela abre — e não vazia, para que
/// "não tenho acesso" e "não tem nada" não virem o mesmo pixel (ADR-0033 §6).
/// </summary>
public sealed record RaiaPainel<T>(int Total, IReadOnlyList<T> Itens);

/// <summary>Uma solicitação numa raia do painel.</summary>
public sealed record ItemSolicitacaoPainel(
    /// <summary>Id PÚBLICO — o do exame quando há satélite de imagem, senão o da espinha (ADR-0021).
    /// É o que faz o link para a tela de detalhe funcionar.</summary>
    Guid Id,
    Guid PacienteId,
    string? PacienteNome,
    string? Procedimento,
    DateTime? DataAgendada,
    /// <summary>Texto literal digitado pelo paciente ao avisar que não vem. Só na raia de cancelados.</summary>
    string? MotivoCancelamento,
    /// <summary>Canal da resposta: whatsapp-quickreply | app | whatsapp-link | sandbox.</summary>
    string? Canal,
    DateTime? RespondidoEm,
    DirecaoSolicitacao? Direcao,
    /// <summary>Nome da unidade executante — preenchido só na lente Município, onde não há seta.</summary>
    string? UnidadeNome);

/// <summary>Uma pendência de importação numa raia do painel (ADR-0035).</summary>
public sealed record ItemPendenciaImportacaoPainel(
    Guid Id,
    string? CodigoSolicitacao,
    string? PacienteNome,
    string? Procedimento,
    DateTime? DataAgendada,
    CausaFalhaImportacao Causa,
    string Motivo,
    int Tentativas,
    /// <summary>A linha é resolvível informando o CPF? Só a causa <c>CpfNaoResolvido</c>.</summary>
    bool PodeInformarCpf,
    string? UnidadeNome);

/// <summary>Números tranquilos: quanto está confirmado, por direção. Não é fila — é contagem.</summary>
public sealed record ConfirmadosPainel(int Recebidos, int Enviados, int JanelaDias);

/// <summary>
/// A resposta inteira da tela de início, numa requisição só (ADR-0033 §1). Raia <c>null</c> = sem
/// permissão; raia com <c>Total = 0</c> = com permissão e sem itens (o front omite as duas, mas a
/// distinção existe para diagnóstico e para o badge do menu).
/// </summary>
public sealed record PainelInicioDto(
    LenteEscopoPainel Lente,
    /// <summary>True ⇒ o front renderiza o seletor de lente. False ⇒ não renderiza (não é botão
    /// desabilitado: ausência não gera a pergunta "por que não posso?").</summary>
    bool PodeAlternarLente,
    DirecaoPainel Direcao,
    Guid? UnidadeReferenciaId,
    string? UnidadeReferenciaNome,
    ConfirmadosPainel? Confirmados,
    RaiaPainel<ItemSolicitacaoPainel>? Cancelados,
    RaiaPainel<ItemSolicitacaoPainel>? Aguardando,
    RaiaPainel<ItemPendenciaImportacaoPainel>? PendenciasImportacao,
    /// <summary>Janela (dias) da raia "aguardando resposta". Constante de código, exposta para a tela
    /// poder dizer "exame em até N dias" sem duplicar o número.</summary>
    int JanelaAguardandoDias)
{
    /// <summary>
    /// O número do badge no item "Início" do menu — só o que exige ação. Existe porque quem tem
    /// menu favorito é redirecionado e nunca vê a home: sem badge, o painel nasce invisível
    /// justamente para os usuários mais frequentes (ADR-0033 §8).
    /// </summary>
    public int TotalCritico => (Cancelados?.Total ?? 0) + (PendenciasImportacao?.Total ?? 0);
}
