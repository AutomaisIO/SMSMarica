using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

public sealed record SolicitacaoExameDto(
    Guid Id,
    string AccessionNumber,
    string StudyInstanceUID,
    string? WorklistItemUid,

    Guid PacienteId,
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,

    Guid TipoExameId,
    string TipoExameNome,
    ModalidadeDicom ModalidadeDicom,

    Guid UnidadeId,
    string UnidadeNome,

    Guid? UnidadeSolicitanteId,
    string? UnidadeSolicitanteNome,

    // Solicitante = só o nome (CRM/COREN e vínculo com médico saíram do produto).
    string SolicitanteNome,

    string? CodigoSolicitacao,
    string? ChaveConfirmacao,
    string? Justificativa,

    StatusSolicitacaoExame Status,
    PrioridadeSolicitacao Prioridade,
    string? Observacoes,

    // Data em que o exame foi solicitado (dia de calendário; null quando não informada).
    DateOnly? DataSolicitacao,

    // Data em que a regulação autorizou (dia de calendário; capturada do SISREG para estatística).
    DateOnly? DataRegulacao,

    DateTime? DataAgendada,
    DateTime? IniciadoEm,
    DateTime? RealizadoEm,
    string? ErroIntegracaoPacs,

    DateTime? CanceladoEm,
    string? MotivoCancelamento,

    // Resposta do PACIENTE à notificação (WhatsApp/app) — independente do Status operacional.
    StatusConfirmacaoAgendamento StatusConfirmacao,
    DateTime? ConfirmadoEm,
    string? ConfirmadoCanal,
    DateTime? ConfirmacaoCanceladaEm,
    string? MotivoCancelamentoPaciente,

    // Autorização presencial (recepção entrou com a chave). Habilita o envio ao PACS.
    DateTime? AutorizadoEm,
    Guid? AutorizadoPor,
    // Paciente tem número verificado? (gate do campo de chave na recepção).
    bool PacienteContatoVerificado,

    int TentativasEnvio,
    DateTime? UltimaTentativaEm,
    DateTime? ProximaTentativaEm,

    DateTime CriadoEm,
    DateTime? AtualizadoEm,

    // Data/hora REAL de execução do exame vinda do DICOM (StudyDate/StudyTime) —
    // fonte da verdade da data do exame. Null quando o PACS não trouxe a tag.
    DateTime? DataEstudo,

    // Linha crua do TXT do SISREG que originou a solicitação (proveniência). Null quando
    // não veio de importação. Exibida na tela de detalhe atrás de um botão discreto.
    string? RawSisreg);

/// <summary>
/// Direção da solicitação RELATIVA à unidade ativa da sessão. <c>Recebida</c> = a unidade
/// ativa é a executora (recebe para realizar → seta para dentro); <c>Enviada</c> = a unidade
/// ativa é a solicitante (gerou o pedido → seta para fora). <c>null</c> quando não há unidade
/// de referência única (ex.: admin vendo todas, ou visão do conjunto de unidades).
/// </summary>
public enum DirecaoSolicitacao
{
    Recebida = 1,
    Enviada = 2,
}

public sealed record SolicitacaoExameListItemDto(
    Guid Id,
    string AccessionNumber,
    // Nº da solicitação no SISREG — exibido embaixo do pedido na lista.
    string? CodigoSolicitacao,
    Guid PacienteId,
    string PacienteNome,
    Guid TipoExameId,
    string TipoExameNome,
    ModalidadeDicom ModalidadeDicom,
    string SolicitanteNome,
    StatusSolicitacaoExame Status,
    // Resposta do paciente à notificação (Pendente/Confirmada/Cancelada) — para a lista.
    StatusConfirmacaoAgendamento StatusConfirmacao,
    // Autorização presencial + erro de PACS — para o status "de fora" derivado.
    DateTime? AutorizadoEm,
    string? ErroIntegracaoPacs,
    PrioridadeSolicitacao Prioridade,
    DateTime? DataAgendada,
    DateTime CriadoEm,
    // Data/hora REAL de execução do exame vinda do DICOM (StudyDate/StudyTime).
    DateTime? DataEstudo,
    // Study do pedido + estado do laudo (habilita o botão "ver laudo" na listagem).
    string StudyInstanceUID,
    Guid? LaudoId,
    bool LaudoAssinado,
    // Direção relativa à unidade ativa (recebida/enviada). Null = sem referência única.
    DirecaoSolicitacao? Direcao,
    // Checks de comunicação (✓ enviado, ✓✓ entregue, ✓✓ azul lida/visualizada, ⚠ falha).
    // Preenchidos no enriquecimento da listagem; null quando não há comunicação da finalidade.
    ComunicacaoChipDto? ChipConfirmacao = null,
    ComunicacaoChipDto? ChipExameLiberado = null,
    ComunicacaoChipDto? ChipLaudoPronto = null,
    // Anamnese (questionário pré-exame) já preenchida — muda a cor do botão na lista.
    bool TemAnamnese = false);
