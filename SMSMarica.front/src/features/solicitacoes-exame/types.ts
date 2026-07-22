import type { ModalidadeDicom } from '@/features/tipos-exame/types';

export type StatusSolicitacao =
  | 'Solicitada'
  | 'Enviada'
  | 'Recebida'
  | 'Agendada'
  | 'EmExecucao'
  | 'Realizada'
  | 'Laudada'
  | 'Cancelada';

export type PrioridadeSolicitacao = 'Eletiva' | 'Prioritaria' | 'Urgente';

/** Resposta do paciente à notificação (WhatsApp/app) — independente do status operacional. */
export type StatusConfirmacaoPaciente = 'Pendente' | 'Confirmada' | 'Cancelada';

/**
 * Direção da solicitação relativa à unidade ativa: `Recebida` = a unidade ativa é a
 * executora (recebe para realizar → seta para dentro); `Enviada` = a unidade ativa é a
 * solicitante (gerou o pedido → seta para fora). null = sem unidade de referência única.
 */
export type DirecaoSolicitacao = 'Recebida' | 'Enviada';

export type SolicitacaoExame = {
  id: string;
  accessionNumber: string;
  studyInstanceUID: string;
  worklistItemUid: string | null;

  pacienteId: string;
  pacienteNome: string;
  pacienteCpf: string | null;
  pacienteCns: string | null;

  tipoExameId: string;
  tipoExameNome: string;
  modalidadeDicom: ModalidadeDicom;

  unidadeId: string;
  unidadeNome: string;

  /** Unidade que solicitou o exame (opcional). */
  unidadeSolicitanteId: string | null;
  unidadeSolicitanteNome: string | null;

  /** Solicitante = só o nome (texto livre). CRM/COREN e vínculo com médico saíram do produto. */
  solicitanteNome: string;

  codigoSolicitacao: string | null;
  chaveConfirmacao: string | null;
  justificativa: string | null;

  status: StatusSolicitacao;
  prioridade: PrioridadeSolicitacao;
  observacoes: string | null;

  /** Data em que o exame foi solicitado (dia de calendário, "yyyy-MM-dd"). */
  dataSolicitacao: string | null;
  /** Data em que a regulação autorizou (dia de calendário) — capturada do SISREG para estatística. */
  dataRegulacao: string | null;

  dataAgendada: string | null;
  iniciadoEm: string | null;
  realizadoEm: string | null;
  erroIntegracaoPacs: string | null;

  canceladoEm: string | null;
  motivoCancelamento: string | null;

  /** Resposta do paciente à notificação: como/quando confirmou ou cancelou + motivo. */
  statusConfirmacao: StatusConfirmacaoPaciente;
  confirmadoEm: string | null;
  confirmadoCanal: string | null;
  confirmacaoCanceladaEm: string | null;
  motivoCancelamentoPaciente: string | null;

  /** Autorização presencial (recepção entrou com a chave). */
  autorizadoEm: string | null;
  autorizadoPor: string | null;
  /** Nome de quem autorizou (resolvido no detalhe). */
  autorizadoPorNome: string | null;
  /** Paciente tem número verificado? (gate do campo de chave). */
  pacienteContatoVerificado: boolean;

  tentativasEnvio: number;
  ultimaTentativaEm: string | null;
  proximaTentativaEm: string | null;

  criadoEm: string;
  atualizadoEm: string | null;

  /** Data/hora REAL de execução do exame vinda do DICOM (StudyDate/StudyTime) — fonte da verdade. */
  dataEstudo: string | null;
  /** Linha crua do TXT do SISREG que originou a solicitação (proveniência). Null se não veio de import. */
  rawSisreg: string | null;
};

export type SolicitacaoExameListItem = {
  id: string;
  accessionNumber: string;
  /** Nº da solicitação no SISREG (exibido embaixo do pedido). */
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string;
  tipoExameId: string;
  tipoExameNome: string;
  modalidadeDicom: ModalidadeDicom;
  /** Unidade EXECUTANTE — exibida sob a modalidade na lista. */
  unidadeNome: string;
  solicitanteNome: string;
  status: StatusSolicitacao;
  /** Resposta do paciente à notificação (Pendente/Confirmada/Cancelada). */
  statusConfirmacao: StatusConfirmacaoPaciente;
  /** Autorização presencial + erro de PACS — para o status "de fora" derivado. */
  autorizadoEm: string | null;
  erroIntegracaoPacs: string | null;
  prioridade: PrioridadeSolicitacao;
  dataAgendada: string | null;
  criadoEm: string;
  /** Study do pedido — usado para localizar o laudo. */
  studyInstanceUID: string;
  /** Laudo "atual" (maior versão finalizada) do estudo, se houver. */
  laudoId: string | null;
  /** True quando esse laudo já está assinado digitalmente (habilita o botão). */
  laudoAssinado: boolean;
  /** Direção relativa à unidade ativa (recebida/enviada). null = sem referência única. */
  direcao: DirecaoSolicitacao | null;
  /** Checks de comunicação (✓ enviado, ✓✓ entregue, ✓✓ azul lida/visualizada, ⚠ falha). */
  chipConfirmacao: ComunicacaoChip | null;
  chipExameLiberado: ComunicacaoChip | null;
  chipLaudoPronto: ComunicacaoChip | null;
  /** Anamnese (questionário pré-exame) já preenchida — muda a cor do botão na lista. */
  temAnamnese: boolean;
};

/** Resumo da comunicação para os checks na lista. */
export type ComunicacaoChip = {
  status: 'Pendente' | 'Enviada' | 'Entregue' | 'Lida' | 'Falha' | 'SemTelefoneValido';
  visualizado: boolean;
  motivo: string | null;
};

// ---- Histórico do processo de comunicação (detalhe) ----

export type HistoricoComunicacao = {
  id: string;
  finalidade: 'ConfirmacaoAgendamento' | 'ExameLiberado' | 'LaudoPronto';
  status: ComunicacaoChip['status'];
  telefone: string | null;
  tentativas: number;
  criadoEm: string;
  enviadoEm: string | null;
  entregueEm: string | null;
  lidoEm: string | null;
  visualizadoEm: string | null;
  motivoFalha: string | null;
  erroMeta: string | null;
};

export type HistoricoContato = {
  id: string;
  meio: 'Ligacao' | 'WhatsApp' | 'Presencial' | 'Outro';
  resultado: 'Atendeu' | 'NaoAtendeu' | 'CaixaPostal' | 'NumeroInvalido' | 'Outro';
  observacao: string | null;
  criadoEm: string;
  registradoPorNome: string | null;
};

export type HistoricoSolicitacao = {
  comunicacoes: HistoricoComunicacao[];
  contatos: HistoricoContato[];
};

export type FiltroSolicitacoes = {
  status?: StatusSolicitacao;
  pacienteId?: string;
  unidadeId?: string;
  tipoExameId?: string;
  dataInicial?: string;
  dataFinal?: string;
  accessionNumber?: string;
  /** Busca livre: nome, CPF, CNS ou nº do pedido/accession/código. */
  busca?: string;
  limite?: number;
};

export type CadastrarSolicitacaoPayload = {
  pacienteId: string;
  tipoExameId: string;
  unidadeId: string;
  unidadeSolicitanteId: string | null;
  solicitanteNome: string;
  codigoSolicitacao: string | null;
  chaveConfirmacao: string | null;
  justificativa: string | null;
  prioridade: PrioridadeSolicitacao;
  observacoes: string | null;
  dataAgendada: string | null;
  /** Data da solicitação (dia de calendário, "yyyy-MM-dd"); opcional. */
  dataSolicitacao: string | null;
};

export type AtualizarSolicitacaoPayload = {
  tipoExameId: string;
  unidadeId: string;
  unidadeSolicitanteId: string | null;
  solicitanteNome: string;
  codigoSolicitacao: string | null;
  chaveConfirmacao: string | null;
  justificativa: string | null;
  prioridade: PrioridadeSolicitacao;
  observacoes: string | null;
  dataAgendada: string | null;
  /** Data da solicitação (dia de calendário, "yyyy-MM-dd"); opcional. */
  dataSolicitacao: string | null;
};

/** Estação (equipamento) elegível para executar o exame — usada na autorização. */
export type EquipamentoExame = {
  id: string;
  nome: string;
  /** AE Title configurado no aparelho — exibido para conferência. */
  aeTitle: string;
  /** Já é a estação gravada neste exame. */
  selecionado: boolean;
};
