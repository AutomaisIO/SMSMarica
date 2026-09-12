namespace SMSMais.Data.Entities.Enums;

/// <summary>Desfecho de um aviso de erro da plataforma (ver <c>AlertaEnvio</c>).</summary>
public enum SituacaoAlertaEnvio
{
    /// <summary>Saiu para todos os destinatários.</summary>
    Enviado = 1,

    /// <summary>Saiu para parte dos destinatários.</summary>
    Parcial = 2,

    /// <summary>Não saiu para ninguém — o motivo está em <c>Resultado</c>.</summary>
    Falhou = 3,

    /// <summary>Não havia telefone cadastrado.</summary>
    SemDestinatario = 4,

    /// <summary>Teto diário de avisos atingido (proteção de custo); contado, não enviado.</summary>
    TetoDiario = 5,
}
