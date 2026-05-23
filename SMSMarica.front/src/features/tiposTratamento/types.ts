export type TipoTratamentoListItem = {
  id: string;
  nome: string;
  codigo: string;
  ativo: boolean;
};

export type TipoTratamento = TipoTratamentoListItem & {
  criadoEm: string;
};

export type CadastrarTipoTratamentoPayload = {
  nome: string;
  codigo: string;
};

export type AtualizarTipoTratamentoPayload = {
  nome: string;
  codigo: string;
  ativo: boolean;
};
