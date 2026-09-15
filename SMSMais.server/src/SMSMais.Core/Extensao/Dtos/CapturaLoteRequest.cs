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
    IReadOnlyList<CapturaContagemDto> PorCaminho,
    IReadOnlyList<CapturaContagemDto> PorEtapa,
    IReadOnlyList<CapturaRecenteDto> Ultimas);

public sealed record CapturaInstalacaoDto(string InstallId, string? UltimaVersao, long Total, DateTime UltimoEm);

public sealed record CapturaContagemDto(string Chave, long Total);

public sealed record CapturaRecenteDto(
    DateTime CriadoEm, DateTime? OcorridoEm, string? OperadorSisreg,
    string Kind, string? Metodo, string? Caminho, string? Etapa, string? Evento, bool Escrita, string? Status);

/// <summary>Estrutura observada nas ações de escrita, SEM PII: só nomes de campos do envio e
/// quais rótulos aparecem na resposta. Serve para decidir se dá para montar uma Solicitação
/// completa a partir do que a extensão captura.</summary>
public sealed record CapturaEstruturaDto(
    IReadOnlyList<EstruturaEnvioDto> Envios,
    IReadOnlyList<EstruturaRespostaDto> Respostas);

public sealed record EstruturaEnvioDto(string Caminho, string? Etapa, int Amostras, IReadOnlyList<string> Campos);

public sealed record EstruturaRespostaDto(string Caminho, int Amostras, IReadOnlyList<string> RotulosPresentes);

/// <summary>Trechos REDIGIDOS (CPF/CNS/telefone mascarados) da resposta de uma marcação, só nas
/// vizinhanças dos rótulos não-sensíveis (número, chave, procedimento, unidade) — para escrever o
/// parser sem expor nome/identificadores de paciente.</summary>
public sealed record CapturaAmostraRespostaDto(string? Caminho, DateTime? Quando, IReadOnlyList<AmostraTrechoDto> Trechos);

public sealed record AmostraTrechoDto(string Rotulo, string Trecho);
