export const PARENTESCOS = [
  'Mae',
  'Pai',
  'Filho',
  'Conjuge',
  'Irmao',
  'OutroParente',
  'Cuidador',
  'Outro',
] as const;
export type Parentesco = (typeof PARENTESCOS)[number];

export const ROTULO_PARENTESCO: Record<Parentesco, string> = {
  Mae: 'Mãe',
  Pai: 'Pai',
  Filho: 'Filho(a)',
  Conjuge: 'Cônjuge',
  Irmao: 'Irmão(ã)',
  OutroParente: 'Outro parente',
  Cuidador: 'Cuidador(a)',
  Outro: 'Outro',
};

/** Pessoa que pode acompanhar o paciente no transporte. A lista é do paciente. */
export type Acompanhante = {
  id: string;
  pacienteId: string;
  cpf: string;
  nome: string;
  dataNascimento: string;
  parentesco: Parentesco | null;
  telefone: string | null;
  /** A pessoa também é paciente na base. */
  tambemEPaciente: boolean;
  origem: 'Painel' | 'App';
  criadoEm: string;
};

/** Quem é a dona do CPF, conferida pelo nascimento — para confirmar o nome antes de cadastrar. */
export type ConsultaAcompanhante = {
  cpf: string;
  nome: string;
  dataNascimento: string;
  jaCadastrado: boolean;
};

export type AdicionarAcompanhantePayload = {
  cpf: string;
  dataNascimento: string;
  parentesco: Parentesco | null;
  telefone: string | null;
};
