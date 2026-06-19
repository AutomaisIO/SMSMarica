import { http } from './httpClient';

export type Perfil = {
  id: string;
  nome: string;
  nomeSocial: string | null;
  cpf: string;
  cns: string | null;
  dataNascimento: string | null;
  email: string | null;
  telefonePrincipal: string | null;
  telefoneCelular: string | null;
  telefoneResidencial: string | null;
  fotoBase64: string | null;
};

export type AtualizarContato = {
  email: string | null;
  telefonePrincipal: string | null;
  telefoneCelular: string | null;
  telefoneResidencial: string | null;
};

export type Translado = { id: string; data: string; destino: string; status: string };
export type Atendimento = {
  id: string;
  data: string;
  estabelecimento: string;
  profissional: string;
  descricao: string;
};
export type Exame = { id: string; data: string; nome: string; status: string };
export type Laudo = { id: string; data: string; titulo: string; status: string };

export const api = {
  perfil: () => http.get<Perfil>('/auth/paciente/me').then((r) => r.data),
  salvarContato: (body: AtualizarContato) => http.put('/auth/paciente/me/contato', body),
  salvarFoto: (fotoBase64: string | null) => http.put('/auth/paciente/me/foto', { fotoBase64 }),
  logout: () => http.post('/auth/paciente/logout'),
  translados: () => http.get<Translado[]>('/auth/paciente/meus-translados').then((r) => r.data),
  atendimentos: () => http.get<Atendimento[]>('/auth/paciente/atendimentos').then((r) => r.data),
  exames: () => http.get<Exame[]>('/auth/paciente/exames').then((r) => r.data),
  laudos: () => http.get<Laudo[]>('/auth/paciente/laudos').then((r) => r.data),
};
