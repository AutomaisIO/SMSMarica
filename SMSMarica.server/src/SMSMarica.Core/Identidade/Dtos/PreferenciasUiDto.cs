namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Preferências de UI do usuário, persistidas por usuário (jsonb em <c>usuario.preferencias_ui</c>).
/// Campos além de <see cref="MenuDefaults"/> são opcionais: no PUT, cada campo nulo é
/// preservado (merge no servidor), permitindo que telas diferentes gravem só a sua parte.
/// </summary>
/// <param name="MenuDefaults">Tela default de cada seção do menu (id da seção → rota).</param>
/// <param name="AlturaComposerChat">Altura (px) da caixa de digitação do chat de conversas.</param>
/// <param name="EnviarComEnter">Se Enter envia a mensagem no chat (Shift+Enter quebra linha). Quando desligado, Enter também quebra linha.</param>
public sealed record PreferenciasUiDto(
    Dictionary<string, string> MenuDefaults,
    int? AlturaComposerChat = null,
    bool? EnviarComEnter = null);
