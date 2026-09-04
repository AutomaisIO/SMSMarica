namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Situação da escala como o próprio SISREG a classifica (coluna STATUS do export de
/// <c>cons_escalas</c>). O valor inteiro é persistido — não renumerar.
///
/// <para><b>Cuidado que custa caro:</b> <see cref="Ativa"/> <b>não</b> quer dizer "vale hoje".
/// Medido em 04/09/2026 sobre o arquivo real: das 1.639 ativas, <b>705 só começam no futuro</b> e
/// 934 estavam vigentes. Quem quer a oferta de hoje precisa cruzar o status <i>com</i> a vigência;
/// filtrar só por <c>Ativa</c> infla a oferta em quase o dobro.</para>
/// </summary>
public enum StatusEscalaSisreg
{
    /// <summary>Escala ligada no SISREG. Pode ainda não ter começado — conferir a vigência.</summary>
    Ativa = 1,

    /// <summary>Desligada pelo operador, mas ainda dentro do período de vigência.</summary>
    Inativa = 2,

    /// <summary>Vigência já passou. É a maior parte do arquivo (13.457 de 17.469 na medição).</summary>
    Expirada = 3,

    /// <summary>Removida no SISREG. Continua saindo no export.</summary>
    Excluida = 4,
}
