export type Especialidade = {
  id: string;
  nome: string;
  codigoCbo: string | null;
  ativo: boolean;
  criadoEm: string;
};

export type EspecialidadeListItem = {
  id: string;
  nome: string;
  codigoCbo: string | null;
  ativo: boolean;
};

export type SalvarEspecialidadePayload = {
  nome: string;
  codigoCbo?: string | null;
  ativo?: boolean;
};
