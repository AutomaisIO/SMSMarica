namespace SMSMarica.Core.Pacientes.Dtos;

/// <summary>
/// Adiciona um telefone aos contatos do paciente como um <c>Patient.telecom</c>
/// nativo (append) — sem substituir os existentes. Idempotente: se o número já
/// estiver lá, não duplica.
/// </summary>
public sealed record AdicionarTelefoneRequest(
    string Numero,
    /// <summary>"celular" (default), "residencial" ou "comercial".</summary>
    string? Tipo = null);
