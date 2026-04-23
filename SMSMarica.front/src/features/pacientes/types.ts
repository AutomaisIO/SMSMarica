export type PacienteListItem = {
  id: string;
  nomeCompleto: string;
  cpf: string;
  ativo: boolean;
};

export type Paciente = {
  id: string;
  nomeCompleto: string;
  cpf: string;
  cns: string | null;
  latitude: number;
  longitude: number;
  ativo: boolean;
  cadastradoEm: string;
};

export type CadastrarPacientePayload = {
  nomeCompleto: string;
  cpf: string;
  cns?: string;
  latitude: number;
  longitude: number;
};

export type AtualizarPacientePayload = {
  nomeCompleto: string;
  cns?: string;
  latitude: number;
  longitude: number;
};
