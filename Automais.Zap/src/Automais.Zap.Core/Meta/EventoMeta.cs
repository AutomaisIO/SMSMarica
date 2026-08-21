namespace Automais.Zap.Core.Meta;

/// <summary>
/// Um <c>change</c> dentro do payload, com a coordenada que permite recortá-lo depois.
/// Só metadado de roteamento: nada aqui é conteúdo de mensagem.
/// </summary>
/// <param name="IndiceEntry">Posição em <c>entry[]</c>.</param>
/// <param name="IndiceChange">Posição em <c>entry[].changes[]</c>.</param>
/// <param name="PhoneNumberId">De <c>value.metadata.phone_number_id</c>. Nulo em eventos que não são de número (ex.: status de template).</param>
/// <param name="WabaId">O <c>entry.id</c> — usado como rota de reserva quando não há número.</param>
/// <param name="Field">O <c>change.field</c>: "messages", "message_template_status_update"…</param>
public sealed record EventoMeta(
    int IndiceEntry,
    int IndiceChange,
    string? PhoneNumberId,
    string? WabaId,
    string Field);
