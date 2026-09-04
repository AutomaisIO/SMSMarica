namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Situação de um item de treinamento do robô. Valor inteiro estável (persistido) — não renumerar.
/// </summary>
public enum StatusTreinamentoRobo
{
    /// <summary>Crítica registrada pelo atendente; ninguém mandou treinar ainda.</summary>
    Aberto = 1,

    /// <summary>O agente treinador está rodando (proposta + adversários + juiz).</summary>
    Analisando = 2,

    /// <summary>Parou numa pendência: só volta a andar depois que um humano responder.</summary>
    AguardandoHumano = 3,

    /// <summary>Análise concluída e alteração aplicada, mas a simulação de verificação não rodou.</summary>
    SimulacaoPendente = 4,

    /// <summary>Ciclo completo: analisado, aplicado (ou dispensado) e simulado.</summary>
    Concluido = 5,

    /// <summary>Analisado e decidido que não há mudança a fazer (crítica improcedente).</summary>
    Descartado = 6,

    /// <summary>O agente falhou (erro de API, teto de iterações, resposta inválida).</summary>
    Falhou = 7,
}

/// <summary>
/// Natureza de uma pendência de um item de treinamento — o que o agente precisa do humano.
/// Valor inteiro estável.
/// </summary>
public enum TipoPendenciaTreinamento
{
    /// <summary>Decisão de regra de negócio que o agente não pode tomar por conta própria
    /// (inclui mudança de persona, horário, limiar e liga/desliga de comando).</summary>
    RegraNegocio = 1,

    /// <summary>A correção exige mexer em código (guardrail, catálogo de comandos, comando novo,
    /// motor). Precisa de autorização explícita antes de virar trabalho de desenvolvimento.</summary>
    AlteracaoCodigo = 2,
}

/// <summary>Situação de uma pendência de treinamento. Valor inteiro estável.</summary>
public enum StatusPendenciaTreinamento
{
    /// <summary>Aguardando o humano abrir o item e responder.</summary>
    Aberta = 1,

    /// <summary>Respondida — a análise pode retomar de onde parou.</summary>
    Respondida = 2,

    /// <summary>Dispensada pelo humano (não se aplica, ou decidiu não seguir).</summary>
    Dispensada = 3,
}

/// <summary>
/// O que uma alteração do agente treinador mexeu. Só existem os dois alvos que o agente pode
/// aplicar sozinho — o resto vira pendência. Valor inteiro estável.
/// </summary>
public enum AlvoAlteracaoTreinamento
{
    /// <summary>Regra local do assunto (<c>robo_assunto_treino</c>).</summary>
    TreinoAssunto = 1,

    /// <summary>Condição de ativação do assunto — roteamento (<c>robo_assunto_condicao</c>).</summary>
    CondicaoAssunto = 2,
}

/// <summary>Operação de uma alteração do agente treinador. Valor inteiro estável.</summary>
public enum OperacaoAlteracaoTreinamento
{
    Criar = 1,
    Atualizar = 2,

    /// <summary>Desativação lógica (<c>Ativo = false</c>) — nunca DELETE, para permitir desfazer.</summary>
    Desativar = 3,
}

/// <summary>
/// Veredito de uma simulação de verificação — se o modelo treinado atual passou a tratar bem o
/// caso que gerou a crítica. Valor inteiro estável.
/// </summary>
public enum VereditoSimulacaoTreinamento
{
    /// <summary>A resposta simulada atende a crítica.</summary>
    Passou = 1,

    /// <summary>A resposta simulada ainda incorre no problema apontado.</summary>
    Falhou = 2,

    /// <summary>Melhorou mas não resolve, ou o juiz não conseguiu decidir.</summary>
    Duvidoso = 3,
}
