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

  dataAgendada: string | null;
  iniciadoEm: string | null;
  realizadoEm: string | null;
  erroIntegracaoPacs: string | null;

  canceladoEm: string | null;
  motivoCancelamento: string | null;

  tentativasEnvio: number;
  ultimaTentativaEm: string | null;
  proximaTentativaEm: string | null;

  criadoEm: string;
  atualizadoEm: string | null;

  /** Data/hora REAL de execução do exame vinda do DICOM (StudyDate/StudyTime) — fonte da verdade. */
  dataEstudo: string | null;
};

export type SolicitacaoExameListItem = {
  id: string;
  accessionNumber: string;
  pacienteId: string;
  pacienteNome: string;
  tipoExameId: string;
  tipoExameNome: string;
  modalidadeDicom: ModalidadeDicom;
  solicitanteNome: string;
  status: StatusSolicitacao;
  prioridade: PrioridadeSolicitacao;
  dataAgendada: string | null;
  criadoEm: string;
  /** Study do pedido — usado para localizar o laudo. */
  studyInstanceUID: string;
  /** Laudo "atual" (maior versão finalizada) do estudo, se houver. */
  laudoId: string | null;
  /** True quando esse laudo já está assinado digitalmente (habilita o botão). */
  laudoAssinado: boolean;
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
};
