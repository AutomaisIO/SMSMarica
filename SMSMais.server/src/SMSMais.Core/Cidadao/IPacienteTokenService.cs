namespace SMSMais.Core.Cidadao;

/// <summary>
/// Emite o JWT do paciente (cidadão) após autenticação. Implementado na Api
/// (assina com a mesma chave/issuer do token de usuário). Claims: <c>tipo=cidadao</c>,
/// <c>sub</c> = id do paciente no hub FHIR, <c>jti</c> = id da sessão (single-device),
/// <c>exp</c> casado com a validade da sessão.
/// </summary>
public interface IPacienteTokenService
{
    string Gerar(Guid pacienteId, string nome, string? cpf, Guid sessaoJti, DateTime expiraEm);
}
