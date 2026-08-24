using SMSMais.Core.Estatisticas.Dtos;

namespace SMSMais.Core.Estatisticas;

/// <summary>
/// Agregações gerenciais das comunicações WhatsApp (Central de Atendimento + envios automáticos).
/// Só leitura: nunca retorna conteúdo de mensagem nem identidade de paciente.
/// </summary>
public interface IEstatisticasService
{
    /// <summary>
    /// Retrato do WhatsApp no intervalo [de, ate] (inclusive nas duas pontas, por dia).
    /// Exclui mensagens em modo simulado.
    /// </summary>
    Task<EstatisticasWhatsAppDto> ObterWhatsAppAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);

    /// <summary>
    /// Agregados dos exames de imagem no intervalo [de, ate] (âncora <c>dataRef</c>, ver
    /// <see cref="Dtos.EstatisticasExamesImagemDto"/>), no escopo de unidade do usuário atual e,
    /// opcionalmente, restrito a uma única unidade EXECUTANTE. Só contagens/médias — sem PII.
    /// </summary>
    Task<EstatisticasExamesImagemDto> ObterExamesImagemAsync(
        DateOnly de, DateOnly ate, Guid? unidadeId, SMSMais.Data.Entities.Enums.ModalidadeDicom? modalidade,
        Guid? tipoExameId, CancellationToken ct = default);

    /// <summary>
    /// Materializa a lista ANALÍTICA de imagem (exames e/ou laudos) que sustenta os agregados, para
    /// exportação. Mesmo recorte/escopo de <see cref="ObterExamesImagemAsync"/>. SEM PII de paciente
    /// (só números do exame/solicitação/laudo). Registra auditoria de quem exportou e o quê.
    /// </summary>
    Task<ExportacaoImagemDto> ObterExportacaoImagemAsync(
        DateOnly de, DateOnly ate, Guid? unidadeId, SMSMais.Data.Entities.Enums.ModalidadeDicom? modalidade,
        Guid? tipoExameId, ConteudoExportacaoImagem conteudo, CancellationToken ct = default);

    /// <summary>
    /// Lista de FATURAMENTO dos exames de imagem realizados no período (uma linha por exame, da menor
    /// para a maior data de realização). Mesmo recorte/escopo das demais visões. CARREGA PII do paciente
    /// (nome/CPF/CNS/nascimento/CEP/celular) porque o faturamento precisa identificar o cidadão —
    /// ticket #74. Registra auditoria de quem exportou. O front monta o .xlsx formatado.
    /// </summary>
    Task<IReadOnlyList<ExameFaturamentoDto>> ObterFaturamentoImagemAsync(
        DateOnly de, DateOnly ate, Guid? unidadeId, SMSMais.Data.Entities.Enums.ModalidadeDicom? modalidade,
        Guid? tipoExameId, CancellationToken ct = default);
}
