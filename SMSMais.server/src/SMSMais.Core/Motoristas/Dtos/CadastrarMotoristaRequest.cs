using SMSMais.Core.Common.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Motoristas.Dtos;

public sealed record CadastrarMotoristaRequest(
    string NomeCompleto,
    string Cpf,
    DateOnly? DataNascimento,
    string Cnh,
    string? Email,
    string? Telefone,
    EnderecoDto? Endereco,
    string? FotoBase64 = null,
    string? CategoriaCnh = null,
    RegimeContratacao? RegimeContratacao = null);

public sealed record PromoverMotoristaRequest(
    Guid UsuarioId,
    string Cnh,
    string? CategoriaCnh = null,
    RegimeContratacao? RegimeContratacao = null);
