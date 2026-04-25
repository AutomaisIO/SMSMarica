using SMSMarica.Core.Common.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Identidade.Dtos;

public sealed record AtualizarUsuarioRequest(
    string NomeCompleto,
    string? Cpf,
    string? Telefone,
    EnderecoDto? Endereco,
    PerfilUsuario Perfil);
