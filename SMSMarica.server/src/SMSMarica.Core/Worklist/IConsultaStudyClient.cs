namespace SMSMarica.Core.Worklist;

/// <summary>
/// Estudo do PACS varrido por data (StudyDate), com as três chaves usadas na
/// conciliação PACS-driven: StudyInstanceUID, AccessionNumber (0008,0050) e
/// PatientID (0010,0020).
/// </summary>
public sealed record EstudoPacsRecente(string StudyInstanceUID, string? AccessionNumber, string? PatientId);

/// <summary>
/// Cliente QIDO-RS. Varre estudos por StudyDate (base da conciliação PACS-driven do
/// <c>SincronizadorExamesService</c>) e checa existência/atributos por StudyInstanceUID.
/// </summary>
public interface IConsultaStudyClient
{
    /// <summary>"Este StudyInstanceUID existe no PACS?"</summary>
    Task<bool> StudyExistePorStudyUidAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>
    /// Estudos recentes por <b>StudyDate</b> (0008,0020) no intervalo [<paramref name="inicio"/>,
    /// <paramref name="fim"/>], até <paramref name="limite"/> itens. Base da conciliação
    /// PACS-driven: itera o que CHEGOU (data do exame é intrinsecamente recente) e casa
    /// pelo AccessionNumber — sem depender de quando a solicitação foi criada.
    /// Falha do PACS (transporte/HTTP não-2xx/JSON inválido) LANÇA — o chamador não pode
    /// confundir "PACS fora do ar" com "sem estudos na janela".
    /// </summary>
    Task<IReadOnlyList<EstudoPacsRecente>> BuscarStudiesPorDataAsync(
        DateOnly inicio, DateOnly fim, int limite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Data/hora reais do estudo no PACS — combina StudyDate (0008,0020) e
    /// StudyTime (0008,0030). Retorna <c>null</c> se o estudo não existir ou as
    /// tags estiverem ausentes. O <see cref="DateTime"/> é "wall-clock" local do
    /// equipamento (<see cref="DateTimeKind.Unspecified"/>), sem conversão de fuso.
    /// </summary>
    Task<DateTime?> ObterDataHoraEstudoAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>
    /// AE Title de onde as imagens do estudo vieram — tag privada do dcm4chee
    /// <c>(7777,1037) SendingApplicationEntityTitleOfSeries</c>. É o
    /// <c>Equipamento.IdentificadorDicom</c> que cadastramos, logo diz de qual EQUIPAMENTO (e
    /// portanto de qual UNIDADE) o estudo saiu — inclusive quando ele ainda é órfão.
    ///
    /// <para>Consultado no nível de SÉRIE de propósito: no nível de estudo o dcm4chee aceita a
    /// tag como filtro mas devolve o campo vazio. Retorna <c>null</c> se o estudo não existir,
    /// a tag estiver ausente ou o PACS estiver indisponível.</para>
    /// </summary>
    Task<string?> ObterAeOrigemAsync(string studyInstanceUID, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nome do paciente (0010,0010, VR PN) como veio no DICOM do estudo, já limpo
    /// para exibição (componentes "^" viram espaço). Retorna <c>null</c> se o estudo
    /// não existir, a tag estiver ausente/vazia ou o PACS estiver indisponível.
    /// Usado como rótulo TEMPORÁRIO no laudo de um exame ainda sem vínculo.
    /// </summary>
    Task<string?> ObterNomePacienteAsync(string studyInstanceUID, CancellationToken cancellationToken = default);
}
