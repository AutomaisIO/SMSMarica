export type AvaliacaoListItem = {
  id: string;
  sessaoId: string;
  nota: number;
};

export type Avaliacao = {
  id: string;
  sessaoId: string;
  nota: number;
  comentario: string | null;
  criadoEm: string;
};

export type RegistrarAvaliacaoPayload = {
  sessaoId: string;
  nota: number;
  comentario?: string;
};

export type AtualizarAvaliacaoPayload = {
  nota: number;
  comentario?: string;
};
