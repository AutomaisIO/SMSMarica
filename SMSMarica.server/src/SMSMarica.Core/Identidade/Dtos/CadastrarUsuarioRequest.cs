namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Cadastra um Usuario sem papel (admin/operador). Para criar cidadão/médico/
/// motorista, use os endpoints específicos (<c>POST /pacientes</c>, <c>/medicos</c>,
/// <c>/motoristas</c>) — eles criam o agregado de identidade no schema certo e
/// fazem o vínculo via <c>Usuario.PatientId / PractitionerId / MotoristaId</c>.
/// </summary>
public sealed record CadastrarUsuarioRequest(
    /// <summary>Nome de exibição na UI (vai pra <c>usuario.nome_exibicao</c>).</summary>
    string NomeCompleto,
    string Email,
    IReadOnlyList<Guid>? PerfilIds = null,
    string? Senha = null);
