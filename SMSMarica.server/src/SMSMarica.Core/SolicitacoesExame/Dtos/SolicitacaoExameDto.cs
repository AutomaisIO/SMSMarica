using SMSMais.Data.Entities.Enums;

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
    // Nome de quem autorizou presencialmente (resolvido no detalhe; null na listagem). Ticket #30.
    string? AutorizadoPorNome,
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
    string? RawSisreg,

    // Equipamento (estação) de destino do envio ao PACS — define o ScheduledStationAETitle
    // e o WorklistLabel do item MWL. Null enquanto não escolhido/deduzido. Exibido no detalhe
    // ("informação enviada ao PACS") e é o alvo da troca de destino (ticket #72).
    Guid? EquipamentoId,
    string? EquipamentoNome,
    string? EquipamentoAeTitle,

    // Dispensa de verificação do contato (paciente consentiu em não validar o WhatsApp). Quando
    // ativa, libera a autorização presencial mesmo sem número verificado. Resolvida só no
    // DETALHE (a listagem não precisa) — daí os defaults.
    bool PacienteContatoDispensado = false,
    // Motivo em texto pronto para a tela (a descrição livre quando o motivo é "Outro").
    string? PacienteContatoDispensaMotivo = null,
    // A dispensa deixa resultado/laudo saírem por WhatsApp? false = entrega presencial.
    bool PacienteContatoDispensaPermiteEnvio = false);

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
    // CPF do paciente. NULO quando o cidadão entrou pela importação do SISREG ancorado só no
    // CNS — a lista pinta a linha de laranja e a recepção cobra o CPF antes de abrir.
    string? PacienteCpf,
    Guid TipoExameId,
    string TipoExameNome,
    ModalidadeDicom ModalidadeDicom,
    // Unidade EXECUTANTE — exibida sob a modalidade na lista (ticket #14).
    string UnidadeNome,
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

/// <summary>Página da listagem de solicitações (paginação offset + total para os controles).</summary>
public sealed record PaginaSolicitacoesDto(
    IReadOnlyList<SolicitacaoExameListItemDto> Itens,
    int Total,
    int Pagina,
    int Tamanho);

/// <summary>
/// Resultado de informar o CPF do paciente de uma solicitação.
/// <paramref name="Repontado"/> = o CPF já era de outro cadastro e a solicitação passou a apontar
/// para ele; a tela precisa avisar, porque o nome do paciente pode ter mudado na frente do operador.
/// </summary>
public sealed record DefinirCpfPacienteResultadoDto(
    Guid PacienteId,
    string NomePaciente,
    string Cpf,
    bool Repontado);

/// <summary>CPF informado pela recepção para o paciente de uma solicitação.</summary>
public sealed record DefinirCpfPacienteRequest(string Cpf);
