/** O trecho da viagem avaliado: a ida (até a unidade) ou o resgate (de volta para casa). */
export type TrechoViagem = 'Ida' | 'Resgate';

/** Uma nota com a viagem: dia, paciente, o trecho, quem dirigiu e em qual veículo (guardados na hora
 * da avaliação) e por onde chegou — `AppCidadao` = o próprio paciente pelo app; `Painel` = a equipe. */
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
  trecho: TrechoViagem;
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
  trecho?: TrechoViagem;
};

export type AtualizarAvaliacaoPayload = {
  nota: number;
  comentario?: string;
};
