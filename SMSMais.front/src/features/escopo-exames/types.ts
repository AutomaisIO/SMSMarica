import type { ModalidadeDicom } from '@/features/equipamentos/types';

/** Uma linha da aba "Exames de imagem" da unidade. */
export type EscopoExameItem = {
  id: string;
  tipoExameId: string;
  tipoExameNome: string;
  codigoSisreg: string | null;
  modalidadeDicom: ModalidadeDicom;
  unidadeId: string;
  unidadeNome: string;
  enviarParaWorklist: boolean;
  equipamentoId: string | null;
  equipamentoNome: string | null;
  equipamentoAeTitle: string | null;
  /** Aparelhos ATIVOS da unidade que atendem a modalidade deste exame. */
  equipamentosCompativeis: number;
  ativo: boolean;
};

export type AdicionarEscopoPayload = {
  tipoExameId: string;
  unidadeId: string;
  enviarParaWorklist?: boolean;
  equipamentoId?: string | null;
};

export type AtualizarEscopoPayload = {
  enviarParaWorklist: boolean;
  equipamentoId: string | null;
  ativo?: boolean;
};

export type ResultadoBackfillEscopo = {
  paresEncontrados: number;
  criados: number;
  jaExistiam: number;
  comEnvioLigado: number;
};
