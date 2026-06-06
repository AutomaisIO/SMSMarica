using Hl7.Fhir.Model;

namespace SMSMarica.Core.Integracoes.Pep.Fhir;

/// <summary>
/// Escritor genérico de recursos no hub FHIR (Automais.Fhir) usado pela importação de
/// PEPs. Diferente dos clients tipados (paciente/médico), aceita qualquer
/// <see cref="Resource"/> e expõe as operações de upsert/purga que a importação precisa.
/// </summary>
public interface IHubFhirEscritor
{
    /// <summary>POST do recurso; devolve o recurso criado (com <c>Id</c> atribuído pelo hub).</summary>
    Task<Resource> CriarAsync(Resource recurso, CancellationToken ct = default);

    /// <summary>PUT do recurso num id existente; devolve o recurso atualizado.</summary>
    Task<Resource> AtualizarAsync(string tipo, string id, Resource recurso, CancellationToken ct = default);

    /// <summary>DELETE do recurso (ignora 404).</summary>
    Task ExcluirAsync(string tipo, string id, CancellationToken ct = default);

    /// <summary>GET <c>fhir/{tipo}?patient={pacienteId}</c>.</summary>
    Task<Bundle> BuscarPorPacienteAsync(string tipo, string pacienteId, CancellationToken ct = default);

    /// <summary>GET <c>fhir/{tipo}?identifier={system}|{value}</c>.</summary>
    Task<Bundle> BuscarPorIdentifierAsync(string tipo, string system, string value, CancellationToken ct = default);

    /// <summary>GET <c>fhir/{tipo}</c> (lista — usado na purga full-refresh).</summary>
    Task<Bundle> ListarAsync(string tipo, CancellationToken ct = default);
}
