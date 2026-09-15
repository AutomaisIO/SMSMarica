using SMSMais.Core.Common.Dtos;

namespace SMSMais.Core.Identidade.Dtos;

public sealed record CadastrarUsuarioRequest(
    string NomeCompleto,
    string? Email,
    string? Cpf,
    DateOnly? DataNascimento,
    string? Telefone,
    EnderecoDto? Endereco,
    IReadOnlyList<Guid>? PerfilIds = null,
    string? Senha = null,
    string? FotoBase64 = null,
    /// <summary>Quando há senha inicial, exige troca no próximo login. Ignorado sem senha.</summary>
    bool DeveTrocarSenha = false,
    /// <summary>Nome de usuário para login, alternativa ao e-mail/CPF. Opcional.</summary>
    string? Login = null,
    /// <summary>Logins no SISREG. Opcional; cada login pertence a no máximo um usuário.</summary>
    IReadOnlyList<string>? LoginsSisreg = null);
