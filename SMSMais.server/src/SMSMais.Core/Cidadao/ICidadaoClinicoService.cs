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
    /// <summary>
    /// Exames realizados do paciente. Por padrão, só os <b>liberados</b> a ele (ADR-0075): o exame de
    /// aparelho que só libera com o laudo fica fora até o laudo ser aprovado.
    /// <paramref name="visaoDaEquipe"/> inclui os retidos — para quem usa esta lista do lado de dentro
    /// (regulação, acervo visto pela equipe), nunca para o que o paciente vê.
    /// </summary>
    Task<IReadOnlyList<ExameResumoDto>> ListarExamesAsync(
        Guid pacienteId, bool visaoDaEquipe = false, CancellationToken cancellationToken = default);

    /// <summary>Conteúdo de um documento escaneado, se pertencer a um exame do paciente (senão <c>null</c>).</summary>
    Task<AnexoConteudo?> ObterAnexoAsync(Guid pacienteId, Guid anexoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// PDF consolidado das imagens do exame (gera/cacheia), se o exame for do paciente — e, para o
    /// paciente, se já tiver sido liberado a ele (ADR-0075). <paramref name="visaoDaEquipe"/> dispensa a
    /// liberação e não marca o aviso como visualizado (quem abriu foi a equipe, não o paciente).
    /// </summary>
    Task<byte[]?> ObterImagensPdfAsync(
        Guid pacienteId, Guid solicitacaoExameId, bool visaoDaEquipe = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LaudoResumoDto>> ListarLaudosAsync(Guid pacienteId, CancellationToken cancellationToken = default);

    /// <summary>PDF do laudo, se for do paciente E estiver assinado (senão <c>null</c>).</summary>
    Task<PdfDownloadDto?> ObterLaudoPdfAsync(Guid pacienteId, Guid laudoId, CancellationToken cancellationToken = default);

    /// <summary><paramref name="tipo"/>: "consulta", "exame" ou null (ambos). Só agendamentos futuros, não cancelados.</summary>
    Task<IReadOnlyList<AgendamentoResumoDto>> ListarAgendamentosAsync(
        Guid pacienteId, string? tipo, CancellationToken cancellationToken = default);

    /// <summary>Detalhe completo (ticket) de um exame agendado do paciente. <c>null</c> se não for dele.</summary>
    Task<AgendamentoExameDetalheDto?> ObterExameAgendadoAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chave de acesso (confirmação do SISREG) do exame agendado — SÓ no dia do atendimento
    /// (Brasília). Passa pelo comando único de chave: banco primeiro, SISREG se ainda não houver.
    /// </summary>
    Task<ChaveAcessoCidadaoDto> ObterChaveAcessoExameAsync(
        Guid pacienteId, Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>Confirma a presença do paciente no exame agendado (card do app).</summary>
    /// <param name="canal">Vai para <c>ConfirmadoCanal</c>: <c>app</c> quando é o paciente;
    /// <c>sandbox</c> quando é a equipe pelo "Entrar como paciente" — a equipe enxerga que foi teste.</param>
    Task ConfirmarExameAsync(Guid pacienteId, Guid solicitacaoExameId, string canal, CancellationToken cancellationToken = default);

    /// <summary>Registra que o paciente NÃO irá (motivo obrigatório). Não cancela o exame —
    /// sinaliza para a equipe (StatusConfirmacao=Cancelada).</summary>
    Task CancelarExameAsync(Guid pacienteId, Guid solicitacaoExameId, string motivo, string canal, CancellationToken cancellationToken = default);
}
