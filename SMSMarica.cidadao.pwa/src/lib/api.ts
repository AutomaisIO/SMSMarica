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
export type DocumentoAtendimento = {
  id: string;
  tipo: string;
  data: string | null;
  conteudoHtml: string;
};
export type Atendimento = {
  id: string;
  data: string;
  estabelecimento: string;
  profissional: string;
  descricao: string;
  documentos: DocumentoAtendimento[];
};
export type AnexoResumo = { id: string; nome: string; tamanhoBytes: number; paginas: number | null };
export type Exame = {
  id: string;
  data: string;
  nome: string;
  status: string;
  studyInstanceUID: string | null;
  temImagens: boolean;
  documentos: AnexoResumo[];
  laudoId: string | null;
  laudoAssinado: boolean;
};
export type Laudo = { id: string; data: string; titulo: string; status: string };
export type Agendamento = {
  id: string;
  inicioEm: string;
  fimEm: string;
  tipo: 'Consulta' | 'Exame';
  titulo: string;
  profissional: string | null;
  unidade: string | null;
  status: string;
};

export type ConsentimentoStatus = {
  versao: string;
  texto: string;
  aceito: boolean;
  aceitoEm: string | null;
};

export const api = {
  consentimento: () =>
    http.get<ConsentimentoStatus>('/auth/paciente/consentimento').then((r) => r.data),
  aceitarConsentimento: () => http.post('/auth/paciente/consentimento'),
  perfil: () => http.get<Perfil>('/auth/paciente/me').then((r) => r.data),
  salvarContato: (body: AtualizarContato) => http.put('/auth/paciente/me/contato', body),
  salvarFoto: (fotoBase64: string | null) => http.put('/auth/paciente/me/foto', { fotoBase64 }),
  logout: () => http.post('/auth/paciente/logout'),
  translados: () => http.get<Translado[]>('/auth/paciente/meus-translados').then((r) => r.data),
  atendimentos: () => http.get<Atendimento[]>('/auth/paciente/atendimentos').then((r) => r.data),
  exames: () => http.get<Exame[]>('/auth/paciente/exames').then((r) => r.data),
  laudos: () => http.get<Laudo[]>('/auth/paciente/laudos').then((r) => r.data),
  agendamentos: (tipo: 'consulta' | 'exame') =>
    http.get<Agendamento[]>('/auth/paciente/agendamentos', { params: { tipo } }).then((r) => r.data),
};

// URLs de PDF protegido (abertas/baixadas via blob — ver lib/pdf.ts).
export const pdfUrls = {
  laudo: (laudoId: string) => `/auth/paciente/laudos/${laudoId}/pdf`,
  anexo: (anexoId: string) => `/auth/paciente/anexos/${anexoId}/conteudo`,
  exameImagens: (solicitacaoId: string) => `/auth/paciente/exames/${solicitacaoId}/imagens-pdf`,
};
