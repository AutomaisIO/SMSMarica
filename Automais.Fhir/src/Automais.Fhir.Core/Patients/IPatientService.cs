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

    /// <summary>
    /// Funde dois Patients que são a MESMA pessoa: o absorvido passa a apontar para o sobrevivente
    /// e todo o clínico de <c>fhir.*</c> é repontado.
    ///
    /// <para><b>O absorvido NÃO é apagado.</b> Fica legível, com <c>active=false</c> e
    /// <c>link[type=replaced-by]</c> para o sobrevivente — quem chegar pelo id antigo (um link
    /// salvo, uma integração, o app do cidadão) encontra o novo em vez de 404. Fusão junta
    /// prontuário de duas pessoas; se o par estiver errado, apagar impediria desfazer.</para>
    ///
    /// <para>O sobrevivente <b>absorve os identifiers</b> que não tinha (CNS antigo entra como
    /// <c>use=old</c>; chaves de origem como <c>urn:salux:cd_paciente</c> entram inteiras). Sem
    /// isso a próxima carga da fonte procuraria pela chave antiga, não acharia ninguém e criaria
    /// a duplicata de novo — que é como boa parte das 1.195 existentes nasceu.</para>
    ///
    /// <para><b>Escopo:</b> o repontamento alcança só <c>fhir.*</c>. As colunas de
    /// <c>smsmarica.*</c> que guardam id de paciente (laudo, solicitacao, tratamento, conversa…)
    /// pertencem a outra aplicação e a outro schema — têm de ser repontadas por ela, na mesma
    /// janela. Nenhuma delas tem FK, então nada avisa se ficarem para trás.</para>
    /// </summary>
    Task<ResultadoFusao> FundirAsync(Guid sobreviventeId, Guid absorvidoId, CancellationToken ct = default);
}

/// <summary>O que a fusão moveu — para conferir contra o que foi medido antes de executar.</summary>
public sealed record ResultadoFusao(
    Guid SobreviventeId,
    Guid AbsorvidoId,
    int IdentifiersAbsorvidos,
    int Encounters,
    int Conditions,
    int Observations,
    int MedicationRequests,
    int MedicationAdministrations,
    int DocumentReferences)
{
    public int TotalRepontado => Encounters + Conditions + Observations
                                 + MedicationRequests + MedicationAdministrations + DocumentReferences;
}
