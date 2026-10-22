/** Uma nota com a viagem: dia, paciente, quem levou e em qual veículo (guardados na hora da
 * avaliação) e por onde chegou — `AppCidadao` = o próprio paciente pelo app; `Painel` = a equipe. */
export type AvaliacaoListItem = {
  id: string;
  sessaoId: string;
  nota: number;
  dataViagem: string | null;
  pacienteNome: string | null;
  motoristaNome: string | null;
  veiculoPlaca: string | null;
  canal: 'Painel' | 'AppCidadao';
  comentario: string | null;
  criadoEm: string | null;
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
