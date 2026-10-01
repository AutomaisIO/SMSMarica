using SMSMais.Core.Common.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Motoristas.Dtos;

public sealed record MotoristaDto(
    Guid Id,
    Guid UsuarioId,
    string NomeCompleto,
    string Cpf,
    DateOnly? DataNascimento,
    string Cnh,
    string? CategoriaCnh,
    RegimeContratacao? RegimeContratacao,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64,
    bool UsuarioAtivo,
    DateTime CriadoEm);

public sealed record MotoristaListItemDto(
    Guid Id,
    Guid UsuarioId,
    string NomeCompleto,
    string Cpf,
    string? CategoriaCnh,
    RegimeContratacao? RegimeContratacao,
    string? FotoBase64,
    bool UsuarioAtivo);
