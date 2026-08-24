using SMSMais.Core.Anexos.Dtos;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Laudos.Assinatura.Dtos;

namespace SMSMais.Core.Cidadao;

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

    /// <summary>Detalhe completo (ticket) de um exame agendado do paciente. <c>null</c> se não for dele.</summary>
    Task<AgendamentoExameDetalheDto?> ObterExameAgendadoAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>Confirma a presença do paciente no exame agendado (card do app).</summary>
    Task ConfirmarExameAsync(Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>Registra que o paciente NÃO irá (motivo obrigatório). Não cancela o exame —
    /// sinaliza para a equipe (StatusConfirmacao=Cancelada).</summary>
    Task CancelarExameAsync(Guid pacienteId, Guid solicitacaoExameId, string motivo, CancellationToken cancellationToken = default);
}
