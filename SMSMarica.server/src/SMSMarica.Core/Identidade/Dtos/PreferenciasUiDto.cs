namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Preferências de UI do usuário, persistidas por usuário (jsonb em <c>usuario.preferencias_ui</c>).
/// Campos além de <see cref="MenuDefaults"/> são opcionais: no PUT, cada campo nulo é
/// preservado (merge no servidor), permitindo que telas diferentes gravem só a sua parte.
/// </summary>
/// <param name="MenuDefaults">Tela default de cada seção do menu (id da seção → rota). Anulável de propósito: o PUT aceita payload parcial (ex.: só a altura do composer) sem esbarrar no required implícito do [ApiController]; o merge preserva o valor atual.</param>
/// <param name="AlturaComposerChat">Altura (px) da caixa de digitação do chat de conversas.</param>
/// <param name="EnviarComEnter">Se Enter envia a mensagem no chat (Shift+Enter quebra linha). Quando desligado, Enter também quebra linha.</param>
/// <param name="VerComoSolicitante">Na lista de Solicitações de Exame, ver por padrão a visão de SOLICITANTE (o que a unidade pediu) em vez de EXECUTANTE (o que ela realiza). Configurado uma vez, fica salvo no usuário. Ver ticket #84.</param>
public sealed record PreferenciasUiDto(
    Dictionary<string, string>? MenuDefaults,
    int? AlturaComposerChat = null,
    bool? EnviarComEnter = null,
    bool? VerComoSolicitante = null);
