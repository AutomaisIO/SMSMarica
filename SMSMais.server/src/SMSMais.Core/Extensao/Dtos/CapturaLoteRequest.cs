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

/// <summary>Panorama do que a extensão já enviou (monitor do piloto). Sem PII de paciente:
/// não traz campos, payload nem conteúdo — só metadados de operação.</summary>
public sealed record CapturaResumoDto(
    long Total,
    DateTime? UltimoRecebidoEm,
    IReadOnlyList<CapturaInstalacaoDto> Instalacoes,
    IReadOnlyList<CapturaContagemDto> PorKind,
    IReadOnlyList<CapturaContagemDto> PorEvento,
    IReadOnlyList<CapturaRecenteDto> Ultimas);

public sealed record CapturaInstalacaoDto(string InstallId, string? UltimaVersao, long Total, DateTime UltimoEm);

public sealed record CapturaContagemDto(string Chave, long Total);

public sealed record CapturaRecenteDto(
    DateTime CriadoEm, DateTime? OcorridoEm, string? OperadorSisreg,
    string Kind, string? Metodo, string? Caminho, string? Etapa, string? Evento, bool Escrita, string? Status);
