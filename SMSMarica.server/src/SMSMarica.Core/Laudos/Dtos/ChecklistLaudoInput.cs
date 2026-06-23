namespace SMSMarica.Core.Laudos.Dtos;

/// <summary>
/// Respostas do checklist estruturado que originaram o laudo. Opcional — laudos
/// de texto livre não enviam. O servidor é a autoridade do <b>cálculo</b>:
/// recomputa o BI-RADS sugerido a partir de <see cref="Contribuicoes"/> (regra
/// "achado mais suspeito"), guarda <see cref="RespostasJson"/> para reabrir o
/// painel e aceita <see cref="BiRadsFinal"/> como override da profissional.
/// </summary>
public sealed record ChecklistLaudoInput(
    string? RespostasJson,
    IReadOnlyList<string>? Contribuicoes,
    string? BiRadsFinal);
