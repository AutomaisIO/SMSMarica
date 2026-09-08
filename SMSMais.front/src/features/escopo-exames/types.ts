import type { ModalidadeDicom } from '@/features/equipamentos/types';

/**
 * Como a linha é pintada. Derivado no backend, nunca gravado — a tela não recalcula para não
 * divergir do que o worker faz.
 */
export type SituacaoEscopoExame = 'Desligado' | 'Configurado' | 'ADefinir';

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
  situacao: SituacaoEscopoExame;
};

/** Uma linha do painel "Exames a configurar". */
export type PendenciaEscopoExame = {
  id: string;
  unidadeId: string;
  unidadeNome: string;
  tipoExameId: string;
  tipoExameNome: string;
  modalidadeDicom: ModalidadeDicom;
  situacao: SituacaoEscopoExame;
  oQueFalta: string;
  examesParados: number;
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

const ROTULO_SITUACAO: Record<SituacaoEscopoExame, string> = {
  Configurado: 'configurado',
  Desligado: 'a configurar',
  ADefinir: 'sem destino',
};

export function rotuloSituacao(s: SituacaoEscopoExame): string {
  return ROTULO_SITUACAO[s] ?? s;
}
