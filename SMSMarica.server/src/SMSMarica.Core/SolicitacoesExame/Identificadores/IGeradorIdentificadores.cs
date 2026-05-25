namespace SMSMarica.Core.SolicitacoesExame.Identificadores;

/// <summary>
/// Gera identificadores que viajam com o exame ponta-a-ponta (worklist →
/// equipamento → study DICOM no PACS → laudo).
/// </summary>
public interface IGeradorIdentificadores
{
    /// <summary>
    /// AccessionNumber no formato <c>SMS{aaaa}{seq6}</c> (ex.: SMS2026000001).
    /// Sequência por ano, persistida no banco; concorrência tratada por unique
    /// constraint (retry no service em caso de colisão).
    /// </summary>
    Task<string> ProximoAccessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// StudyInstanceUID no formato <c>2.25.{guid-numerico}</c> (DICOM PS3.5 B.2)
    /// — globalmente único sem registro de OID.
    /// </summary>
    string NovoStudyInstanceUid();
}
