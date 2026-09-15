using System.Text.Json;

namespace SMSMais.Core.Extensao.Dtos;

/// <summary>
/// Um lote de capturas enviado pela extensão de navegador. Cada item é o evento como a extensão
/// o montou — mantido cru (<see cref="JsonElement"/>) porque a forma varia por tipo (envio,
/// retorno de tela, AJAX). O serviço extrai as colunas de consulta e guarda o resto em jsonb.
/// </summary>
public sealed record CapturaLoteRequest(
    string InstallId,
    string? Versao,
    DateTime? EnviadoEm,
    List<JsonElement> Itens);

/// <summary>Resultado do recebimento: quantos itens foram gravados.</summary>
public sealed record CapturaLoteResultado(int Gravados);
