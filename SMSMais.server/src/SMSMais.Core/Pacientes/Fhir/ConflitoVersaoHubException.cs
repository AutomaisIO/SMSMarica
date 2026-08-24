namespace SMSMais.Core.Pacientes.Fhir;

/// <summary>
/// O hub FHIR devolveu 409/412 (If-Match): a versão que enviamos está obsoleta — houve edição
/// concorrente entre o nosso GET e o PUT. O chamador deve re-ler e reaplicar (read-modify-write).
/// </summary>
public sealed class ConflitoVersaoHubException(Guid id)
    : Exception($"Conflito de versão ao atualizar Patient/{id} no hub FHIR (edição concorrente).")
{
    public Guid PacienteId { get; } = id;
}
