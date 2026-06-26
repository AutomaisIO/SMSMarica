using SMSMarica.Core.Anexos.Dtos;
using SMSMarica.Core.Cidadao.Dtos;
using SMSMarica.Core.Laudos.Assinatura.Dtos;

namespace SMSMarica.Core.Cidadao;

/// <summary>
/// Leitura clínica do app do cidadão: exames realizados (com documentos escaneados e
/// imagens do PACS), laudos assinados e agendamentos. Toda a posse é verificada pelo
/// <c>pacienteId</c> do token — o paciente só enxerga os próprios dados.
/// </summary>
public interface ICidadaoClinicoService
{
    Task<IReadOnlyList<ExameResumoDto>> ListarExamesAsync(Guid pacienteId, CancellationToken cancellationToken = default);

    /// <summary>Conteúdo de um documento escaneado, se pertencer a um exame do paciente (senão <c>null</c>).</summary>
    Task<AnexoConteudo?> ObterAnexoAsync(Guid pacienteId, Guid anexoId, CancellationToken cancellationToken = default);

    /// <summary>PDF consolidado das imagens do exame (gera/cacheia), se o exame for do paciente.</summary>
    Task<byte[]?> ObterImagensPdfAsync(Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LaudoResumoDto>> ListarLaudosAsync(Guid pacienteId, CancellationToken cancellationToken = default);

    /// <summary>PDF do laudo, se for do paciente E estiver assinado (senão <c>null</c>).</summary>
    Task<PdfDownloadDto?> ObterLaudoPdfAsync(Guid pacienteId, Guid laudoId, CancellationToken cancellationToken = default);

    /// <summary><paramref name="tipo"/>: "consulta", "exame" ou null (ambos). Só agendamentos futuros, não cancelados.</summary>
    Task<IReadOnlyList<AgendamentoResumoDto>> ListarAgendamentosAsync(
        Guid pacienteId, string? tipo, CancellationToken cancellationToken = default);
}
