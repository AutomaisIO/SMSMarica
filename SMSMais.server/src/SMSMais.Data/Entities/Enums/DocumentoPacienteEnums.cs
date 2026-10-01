namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Por onde um <see cref="DocumentoPaciente"/> entrou no acervo. Valor inteiro persistido — não
/// renumerar.
/// </summary>
public enum OrigemDocumentoPaciente
{
    /// <summary>Enviado por alguém da equipe direto no cadastro do paciente.</summary>
    Painel = 1,

    /// <summary>Anexado numa solicitação (regulação, SER, SERNIT) — também fica no cadastro.</summary>
    Solicitacao = 2,

    /// <summary>Chegou pela conversa do WhatsApp e alguém da equipe aceitou.</summary>
    WhatsApp = 3,

    /// <summary>O próprio paciente enviou pelo app.</summary>
    AppCidadao = 4,
}

/// <summary>
/// Situação de um <see cref="DocumentoPaciente"/>. Só <see cref="Aceito"/> fica disponível para
/// anexar numa solicitação: o que veio de fora (app do paciente) espera alguém da equipe olhar.
/// </summary>
public enum SituacaoDocumentoPaciente
{
    /// <summary>Enviado pelo paciente; ninguém da equipe conferiu ainda.</summary>
    Pendente = 1,

    /// <summary>Conferido (ou enviado pela própria equipe) — vale no cadastro.</summary>
    Aceito = 2,
}

/// <summary>
/// O que aconteceu com a mídia (imagem/PDF/áudio/vídeo) que o paciente mandou pelo WhatsApp.
/// Valor inteiro persistido — não renumerar.
/// </summary>
public enum SituacaoMidiaWhatsApp
{
    /// <summary>Chegou; o download pelo Zap ainda não terminou.</summary>
    Recebendo = 1,

    /// <summary>Guardada; esperando alguém da equipe aceitar no cadastro ou descartar.</summary>
    Pendente = 2,

    /// <summary>Alguém da equipe adicionou ao cadastro do paciente.</summary>
    Aceita = 3,

    /// <summary>Alguém da equipe descartou — o arquivo saiu do armazenamento.</summary>
    Descartada = 4,

    /// <summary>
    /// Não guardada: o número já tinha o máximo de mídias pendentes sem ninguém decidir. Trava
    /// contra quem manda arquivo em massa para encher o armazenamento.
    /// </summary>
    Bloqueada = 5,

    /// <summary>O download falhou (mídia expirada na Meta, Zap fora, tipo não aceito, grande demais).</summary>
    Falhou = 6,
}
