namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Natureza de uma mensagem da conversa. <see cref="NotaInterna"/> não é enviada ao WhatsApp
/// (visível só aos operadores). <see cref="Sistema"/> são marcos automáticos (ex.: "conversa
/// encaminhada"). <see cref="Robo"/> é resposta do robô de atendimento (sai ao cidadão, mas
/// não tem autor humano — renderizada com marcação distinta). O valor inteiro é estável
/// (persistido) — não renumerar.
/// </summary>
public enum TipoMensagem
{
    Texto = 1,
    Imagem = 2,
    Documento = 3,
    Audio = 4,
    Video = 5,
    Template = 6,
    NotaInterna = 7,
    Sistema = 8,

    /// <summary>Resposta do robô de atendimento (automação, sem autor humano).</summary>
    Robo = 9,
}
