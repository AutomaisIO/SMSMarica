namespace SMSMarica.Core.Integracoes.Sisreg.Dtos;

/// <summary>
/// Resultado de uma busca no Elasticsearch SISREG: o total de documentos que casam a
/// consulta e os itens desta página (já projetados no DTO do índice).
/// </summary>
public sealed record SisregBuscaResultado<T>(long Total, IReadOnlyList<T> Itens);
