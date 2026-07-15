namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Ciclo de vida da <c>Solicitacao</c> no nível de REGULAÇÃO (o pedido) — distinto do status de
/// EXECUÇÃO de imagem, que vive no satélite <c>ExameImagem</c> (<see cref="StatusSolicitacaoExame"/>,
/// com os estados de worklist/PACS). Ver ADR-0021.
///
/// Transições: Solicitada → Agendada → Realizada; qualquer uma → Cancelada.
/// </summary>
public enum StatusSolicitacao
{
    /// <summary>Pedido registrado (regulado no SISREG ou criado manualmente), ainda sem data firme.</summary>
    Solicitada = 1,

    /// <summary>Com data/hora de atendimento definida (agendada).</summary>
    Agendada = 2,

    /// <summary>Atendimento/execução concluída (o satélite, quando houver, detalha o resultado).</summary>
    Realizada = 3,

    /// <summary>Cancelada pela equipe (distinta da intenção do paciente em StatusConfirmacao).</summary>
    Cancelada = 4,
}
