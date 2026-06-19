namespace SMSMarica.Core.Cidadao;

/// <summary>
/// Emite o JWT do paciente (cidadão) após validação do OTP. Implementado na Api
/// (assina com a mesma chave/issuer do token de usuário). Claim <c>tipo=cidadao</c> e
/// <c>sub</c> = id do paciente no hub FHIR.
/// </summary>
public interface IPacienteTokenService
{
    (string Token, DateTime ExpiraEm) Gerar(Guid pacienteId, string nome, string? cpf);
}
