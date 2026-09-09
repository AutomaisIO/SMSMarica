namespace SMSMais.Core.Pacs;

/// <summary>Identidade que deve passar a constar no objeto DICOM.</summary>
/// <param name="PatientId">(0010,0020) — o que usamos como identidade do paciente no PACS.</param>
/// <param name="PatientName">(0010,0010) já no formato DICOM "SOBRENOME^NOMES".</param>
/// <param name="AccessionNumber">(0008,0050) — o nº do pedido.</param>
/// <param name="IssuerOfPatientId">(0010,0021) — a autoridade do <paramref name="PatientId"/>.
/// Vem preenchido quando o id é o CPF e <c>null</c> quando é o Guid do hub; sem ele o dcm4chee
/// fabrica um issuer a partir do NOME, e o nome vira parte da identidade.</param>
public sealed record IdentidadeDicom(
    string PatientId,
    string PatientName,
    string AccessionNumber,
    DateOnly? DataNascimento = null,
    string? Sexo = null,
    string? IssuerOfPatientId = null);

/// <summary>Resultado da reescrita: o estudo novo que substituiu o original.</summary>
/// <param name="PatientIdOriginal">
/// Identidade que estava DENTRO do objeto antes da reescrita — o que a técnica digitou no
/// equipamento. Lida do primeiro arquivo baixado e devolvida para virar trilha: sem ela, depois da
/// reescrita não sobra prova de como o estudo chegou.
/// </param>
public sealed record EstudoReescrito(
    string StudyInstanceUIDNovo,
    int InstanciasReescritas,
    string? PatientIdOriginal = null,
    string? PatientNameOriginal = null);

/// <summary>
/// Reescreve a identidade de um estudo no dcm4chee — de verdade, dentro do objeto armazenado.
///
/// <para><b>Por que não basta coagir.</b> O spike de 2026-08-11 mediu, contra a produção, que
/// <c>POST /studies/{uid}/patient?PatientID=</c> + <c>PUT /studies/{uid}</c> corrigem
/// <c>PatientID</c> e <c>AccessionNumber</c> no que o WADO devolve, mas o <c>PatientName</c>
/// continua o antigo — e nenhuma <c>updatePolicy</c> muda isso. Meia correção deixa o nome de outra
/// pessoa dentro do arquivo, visível em exportação DICOM e gravação de CD.</para>
///
/// <para><b>O que este cliente faz.</b> Baixa as instâncias, reescreve os atributos de identidade,
/// gera UIDs novos (Study/Series/SOP), re-armazena por STOW-RS e então rejeita o original com
/// <c>113038^DCM</c> ("Incorrect Modality Worklist Entry") e o apaga. UID novo é obrigatório: o
/// antigo está colado ao estudo que está sendo removido.</para>
/// </summary>
public interface IPacsReescritorEstudoClient
{
    /// <summary>
    /// Reescreve o estudo com a identidade informada e devolve o StudyInstanceUID novo.
    ///
    /// <para>O original é <b>rejeitado</b> (IOCM <c>113038</c>) ao final — só depois de o novo estar
    /// confirmado no PACS — e <b>não é mais apagado</b>. Ele fica no acervo, visível pelo AE
    /// <c>IOCM_WRONG_MWL</c>, recuperável por <i>Revoke Rejection</i>, e o expurgo em 90 dias é do
    /// próprio dcm4chee. Até 09/2026 rejeitávamos e apagávamos no mesmo passo: o motivo era
    /// registrado e destruído em seguida (<c>rejected_instance</c> ficava zerado), e associação
    /// errada não tinha volta.</para>
    /// </summary>
    Task<EstudoReescrito> ReescreverIdentidadeAsync(
        string studyInstanceUID, IdentidadeDicom identidade, CancellationToken cancellationToken = default);

    /// <summary>Rejeita (IOCM) e apaga o estudo — a opção "descartar" da correção de identidade.</summary>
    Task DescartarAsync(string studyInstanceUID, CancellationToken cancellationToken = default);
}
