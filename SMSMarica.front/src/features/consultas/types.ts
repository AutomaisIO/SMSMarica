import type { ComunicacaoChip } from '@/features/solicitacoes-exame/types';

export type ConsultaListItem = {
  id: string;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  categoria: string;
  especialidade: string | null;
  unidadeExecutanteNome: string;
  solicitanteNome: string;
  dataAgendada: string | null;
  dataSolicitacao: string | null;
  status: string;
  statusConfirmacao: string;
  /** Estado da confirmação por WhatsApp (mesmo chip da lista de exames). */
  chipConfirmacao: ComunicacaoChip | null;
  /** Direção relativa à unidade ativa: Recebida (executora) / Enviada (solicitante) / null. */
  direcao: 'Recebida' | 'Enviada' | null;
};

export type ConsultaDetalhe = {
  id: string;
  codigoSolicitacao: string | null;
  pacienteId: string;
  pacienteNome: string | null;
  pacienteCpf: string | null;
  pacienteCns: string | null;
  categoria: string;
  especialidade: string | null;
  procedimentoTexto: string | null;
  procedimentoSigtapCodigo: string | null;
  unidadeExecutanteNome: string;
  unidadeSolicitanteNome: string | null;
  solicitanteNome: string;
  dataAgendada: string | null;
  dataSolicitacao: string | null;
  dataRegulacao: string | null;
  status: string;
  statusConfirmacao: string;
  observacoes: string | null;
};

export type FiltroConsultas = {
  pacienteId?: string;
  busca?: string;
  dataInicial?: string;
  dataFinal?: string;
  status?: string;
  limite?: number;
};
