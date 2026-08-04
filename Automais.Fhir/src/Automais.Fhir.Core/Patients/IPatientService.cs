using Hl7.Fhir.Model;

namespace Automais.Fhir.Core.Patients;

/// <summary>Filtros de busca de Patient (search params suportados).</summary>
public sealed record PatientBusca(
    string? Cpf = null,
    string? Cns = null,
    string? Nome = null,
    string? Telefone = null,
    /// <summary>Search param FHIR <c>_id</c>: busca em lote por ids (OR).</summary>
    IReadOnlyCollection<Guid>? Ids = null,
    /// <summary>
    /// Busca EXATA por identifier de qualquer system (<c>urn:klinikos:paciente</c>,
    /// <c>urn:salux:cd_paciente</c>…). É o que permite ao conector reencontrar o paciente SEM
    /// CPF que ele mesmo criou — sem isso, cada ciclo incremental criava uma cópia nova
    /// (medido em 04/08: 25 pacientes viraram 260 recursos).
    /// </summary>
    string? IdentifierSystem = null,
    string? IdentifierValue = null,
    /// <summary>
    /// Busca HUMANA unificada (barra de pesquisa): casa <b>nome</b> (contém, sem acento/caso)
    /// OU <b>CPF/CNS por prefixo</b> (não espera o documento terminar). Ordena
    /// <i>prefixo-primeiro</i> — quem o nome COMEÇA com o termo vem antes. Distinta do match
    /// exato de <see cref="Cpf"/>/<see cref="Cns"/>/identifier, que os conectores usam para
    /// reconciliação e NÃO pode virar prefixo.
    /// </summary>
    string? Termo = null,
    /// <summary>Teto de resultados (segue o "itens por página" da tela). Null = padrão do serviço.</summary>
    int? Limite = null);

/// <summary>
/// Operações sobre o recurso FHIR <c>Patient</c>. Entrada/saída são objetos
/// Firely; a persistência (jsonb + search params) fica encapsulada.
/// </summary>
public interface IPatientService
{
    /// <summary>Cria um novo Patient (gera id e Meta). Equivale ao POST FHIR.</summary>
    Task<Patient> CriarAsync(Patient patient, CancellationToken ct = default);

    /// <summary>Lê um Patient pelo id lógico. Lança se não existir/estiver excluído.</summary>
    Task<Patient> LerAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Substitui um Patient existente (PUT FHIR). Incrementa a versão. Se <paramref name="versaoEsperada"/>
    /// for informada (If-Match) e diferir da versão atual, lança <c>ConflitoVersaoException</c> (409).
    /// </summary>
    Task<Patient> AtualizarAsync(Guid id, Patient patient, int? versaoEsperada = null, CancellationToken ct = default);

    /// <summary>Exclusão lógica (DELETE FHIR).</summary>
    Task ExcluirAsync(Guid id, CancellationToken ct = default);

    /// <summary>Busca Patients pelos filtros, devolvendo um Bundle searchset.</summary>
    Task<Bundle> BuscarAsync(PatientBusca filtro, CancellationToken ct = default);

    /// <summary>
    /// Iteração keyset (por Id) de TODOS os Patients vivos — para manutenção/backfill. Devolve uma
    /// página ordenada por Id (Id &gt; cursor) com <c>link[rel=next]</c> carregando o próximo cursor.
    /// </summary>
    Task<Bundle> ListarParaManutencaoAsync(Guid? cursor, int count, CancellationToken ct = default);
}
