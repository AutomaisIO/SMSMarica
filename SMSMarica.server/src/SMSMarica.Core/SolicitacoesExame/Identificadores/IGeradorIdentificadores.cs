namespace SMSMarica.Core.SolicitacoesExame.Identificadores;

/// <summary>
/// Gera identificadores que viajam com o exame ponta-a-ponta (worklist →
/// equipamento → study DICOM no PACS → laudo).
/// </summary>
public interface IGeradorIdentificadores
{
    /// <summary>
    /// AccessionNumber no formato <c>{aaaa}{seq6}</c> (ex.: 2026000001) — 10 caracteres,
    /// limite aceito pelo equipamento Fuji. Sequência por ano, persistida no banco;
    /// concorrência tratada por unique constraint (retry no service em caso de colisão).
    /// Formato legado <c>SMS{aaaa}{seq6}</c> ainda é reconhecido na geração da sequência.
    /// </summary>
    Task<string> ProximoAccessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// StudyInstanceUID no formato <c>2.25.{guid-numerico}</c> (DICOM PS3.5 B.2)
    /// — globalmente único sem registro de OID.
    /// </summary>
    string NovoStudyInstanceUid();
}
