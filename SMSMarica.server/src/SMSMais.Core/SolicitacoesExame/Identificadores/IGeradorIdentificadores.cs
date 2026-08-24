namespace SMSMais.Core.SolicitacoesExame.Identificadores;

/// <summary>
/// Gera identificadores que viajam com o exame ponta-a-ponta (worklist →
/// equipamento → study DICOM no PACS → laudo).
/// </summary>
public interface IGeradorIdentificadores
{
    /// <summary>
    /// AccessionNumber no formato <c>{AAMMDD}{seq}</c> (ex.: 260624019 = 19º exame
    /// de 24/06/2026), data no fuso de Maricá (UTC-3). A sequência reinicia a cada
    /// dia, com no mínimo 3 dígitos (expande se passar de 999/dia). <= 16 chars
    /// (limite Fuji SH). Concorrência tratada por unique constraint (retry no service).
    /// </summary>
    Task<string> ProximoAccessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// StudyInstanceUID no formato <c>2.25.{guid-numerico}</c> (DICOM PS3.5 B.2)
    /// — globalmente único sem registro de OID.
    /// </summary>
    string NovoStudyInstanceUid();
}
