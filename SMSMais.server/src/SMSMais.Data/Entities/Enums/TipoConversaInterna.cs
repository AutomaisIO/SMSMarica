using System.Text.Json.Serialization;

namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Tipo da conversa da Comunicação interna (ADR-0079). O conversor no próprio tipo faz o enum viajar
/// como STRING também pelo SignalR — o hub usa o JSON dele, não o do MVC, e sem isto o evento chegava
/// com <c>tipo: 2</c> enquanto a API REST mandava <c>"Direta"</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TipoConversaInterna>))]
public enum TipoConversaInterna
{
    /// <summary>Unidade ↔ unidade ("Todos da unidade"): participam todos os vinculados às duas.</summary>
    Unidade = 1,

    /// <summary>Pessoa ↔ pessoa.</summary>
    Direta = 2,
}
