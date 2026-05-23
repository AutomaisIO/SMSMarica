namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Papel profissional impositivo do <see cref="Usuario"/>. Um usuário tem no
/// máximo um papel: ao virar <c>Medico</c>, deixa a lista de "Usuários" e passa
/// a aparecer apenas em "Médicos". Distinto de <see cref="Perfil"/>, que é o
/// agregador RBAC de permissões. Ver ADR-0005.
/// </summary>
public enum TipoPapel
{
    Medico = 1,
    Enfermeiro = 2,
    Motorista = 3,
    Recepcionista = 4,
    Paciente = 5,
    Admin = 6,
}
