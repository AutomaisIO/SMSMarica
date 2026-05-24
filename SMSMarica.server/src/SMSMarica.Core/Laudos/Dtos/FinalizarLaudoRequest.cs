namespace SMSMarica.Core.Laudos.Dtos;

/// <summary>
/// Finalização: aceita o conteúdo final em uma única chamada (evita race com
/// atualização) e congela os snapshots de médico (nome/CRM/UF/RQE).
/// </summary>
public sealed record FinalizarLaudoRequest(
    string Titulo,
    string ConteudoJson,
    string ConteudoHtml);
