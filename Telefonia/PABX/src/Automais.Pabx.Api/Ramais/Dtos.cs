using Automais.Pabx.Api.Data.Entities;

namespace Automais.Pabx.Api.Ramais;

public sealed record RamalDto(
    string Numero,
    int UnidadeId,
    string? UnidadeNome,
    string? Descricao,
    string? Mac,
    MarcaTelefone? Marca,
    string? Modelo,
    string? CallerId,
    bool Ativo,
    OrigemRamal Origem,
    DateTime CriadoEm,
    DateTime AtualizadoEm);

/// <summary>Retornado só na criação/reset: única vez em que o secret sai em claro.</summary>
public sealed record RamalComSecretDto(RamalDto Ramal, string Secret);

public sealed record CriarRamalRequest(
    string Numero,
    int UnidadeId,
    string? Descricao,
    string? Mac,
    MarcaTelefone? Marca,
    string? Modelo,
    string? CallerId);

public sealed record AtualizarRamalRequest(
    int UnidadeId,
    string? Descricao,
    string? Mac,
    MarcaTelefone? Marca,
    string? Modelo,
    string? CallerId,
    bool Ativo);

public sealed record AdotarRamaisRequest(IReadOnlyList<string> Numeros, int UnidadeId);

public sealed record AdocaoResultadoDto(
    IReadOnlyList<string> Adotados,
    IReadOnlyList<string> JaInventariados,
    IReadOnlyList<string> NaoEncontrados);

public sealed record StatusRamalDto(
    string Numero,
    string? Descricao,
    int UnidadeId,
    string? UnidadeNome,
    int? UnidadeDetectadaId,
    OrigemRamal Origem,
    bool Online,
    string? Ip,
    int? LatenciaMs,
    bool EmChamada,
    string? StatusBruto);

public sealed record StatusGeralDto(
    bool AmiDisponivel,
    DateTime ConsultadoEm,
    IReadOnlyList<StatusRamalDto> Ramais);
