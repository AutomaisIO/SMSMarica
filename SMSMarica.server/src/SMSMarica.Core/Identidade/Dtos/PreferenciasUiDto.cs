namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Preferências de UI do usuário. Por enquanto só a tela default de cada seção do
/// menu (id da seção → rota). Estrutura pensada para crescer com outras preferências.
/// </summary>
public sealed record PreferenciasUiDto(Dictionary<string, string> MenuDefaults);
