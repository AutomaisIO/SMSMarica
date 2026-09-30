export type TipoTratamentoListItem = {
  id: string;
  nome: string;
  codigo: string;
  /** Quanto o paciente fica no tratamento, da chegada à liberação. Vale para todo atendimento do tipo. */
  tempoMedioMinutos: number | null;
  ativo: boolean;
};

export type TipoTratamento = TipoTratamentoListItem & {
  criadoEm: string;
};

export type CadastrarTipoTratamentoPayload = {
  nome: string;
  codigo: string;
  tempoMedioMinutos: number;
};

export type AtualizarTipoTratamentoPayload = {
  nome: string;
  codigo: string;
  tempoMedioMinutos: number;
  ativo: boolean;
};
