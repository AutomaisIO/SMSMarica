using System.Text.Json;

namespace SMSMais.Core.EstudoAnotacoes.Dtos;

/// <summary>Versão completa (com payload) — usada ao carregar o estudo ou ao inspecionar histórico.</summary>
public sealed record EstudoAnotacaoVersaoDto(
    Guid Id,
    string StudyInstanceUID,
    int Versao,
    JsonElement Payload,
    Guid UsuarioId,
    string UsuarioNome,
    DateTime CriadoEm,
    string? Comentario
);

/// <summary>Metadados de uma versão sem o payload — usado para listar o histórico de forma leve.</summary>
public sealed record EstudoAnotacaoVersaoResumoDto(
    Guid Id,
    int Versao,
    Guid UsuarioId,
    string UsuarioNome,
    DateTime CriadoEm,
    string? Comentario
);
