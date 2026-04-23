using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Identidade.Dtos;

public sealed record AtualizarUsuarioRequest(
    string NomeCompleto,
    string? Cpf,
    PerfilUsuario Perfil);
