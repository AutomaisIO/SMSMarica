import { http } from './httpClient';
import { comCacheLocal } from './dadosCache';

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
export type PesquisaPublica = {
  unidade: string | null;
  atendimentoEm: string;
  expiraEm: string;
  expirada: boolean;
  jaRespondida: boolean;
  instrumentoVersao: string;
};
export type Agendamento = {
  id: string;
  inicioEm: string;
  fimEm: string;
  tipo: 'Consulta' | 'Exame';
  titulo: string;
  profissional: string | null;
  unidade: string | null;
  status: string;
  // Exame importado (SISREG): habilita confirmar/avisar ausência no card.
  solicitacaoExameId: string | null;
  statusConfirmacao: 'Pendente' | 'Confirmada' | 'Cancelada' | null;
  podeResponder: boolean;
};

export type AgendamentoExameDetalhe = {
  solicitacaoExameId: string;
  tipoExame: string;
  dataAgendada: string | null;
  dataSolicitacao: string | null;
  dataRegulacao: string | null;
  unidadeExecutoraNome: string | null;
  unidadeExecutoraEndereco: string | null;
  unidadeExecutoraTelefone: string | null;
  unidadeSolicitanteNome: string | null;
  solicitanteNome: string | null;
  accessionNumber: string | null;
  codigoSolicitacao: string | null;
  prioridade: string;
  observacoes: string | null;
  statusConfirmacao: 'Pendente' | 'Confirmada' | 'Cancelada';
  confirmadoEm: string | null;
  confirmadoCanal: string | null;
  confirmacaoCanceladaEm: string | null;
  motivoCancelamentoPaciente: string | null;
};

export type TelefoneOtpEmitido = { canal: string; mascara: string | null; expiraEmSegundos: number };
export type TelefoneValidado = { numero: string; validado: boolean; validadoEm: string | null };

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
  // Leituras clínicas: rede-primeiro com fallback OFFLINE (ver lib/dadosCache).
  perfil: () => comCacheLocal('perfil', () => http.get<Perfil>('/auth/paciente/me').then((r) => r.data)),
  salvarContato: (body: AtualizarContato) => http.put('/auth/paciente/me/contato', body),
  // Troca do celular por OTP: envia código ao número NOVO; só efetiva ao confirmar.
  solicitarOtpContato: (numero: string) =>
    http
      .post<TelefoneOtpEmitido>('/auth/paciente/me/contato/otp', { numero })
      .then((r) => r.data),
  confirmarContato: (numero: string, codigo: string) =>
    http
      .post<TelefoneValidado>('/auth/paciente/me/contato/confirmar', { numero, codigo })
      .then((r) => r.data),
  salvarFoto: (fotoBase64: string | null) => http.put('/auth/paciente/me/foto', { fotoBase64 }),
  logout: () => http.post('/auth/paciente/logout'),

  /** Contexto da pesquisa pelo token do WhatsApp — rota pública, sem sessão. */
  pesquisa: (token: string) =>
    http.get<PesquisaPublica>(`/publico/pesquisa/${token}`).then((r) => r.data),
  responderPesquisaPorToken: (token: string, respostas: Record<string, string>) =>
    http.post(`/publico/pesquisa/${token}`, { respostas }),
  /** Mesma pesquisa, entrando pelo histórico do app (já autenticado). */
  responderPesquisaDoAtendimento: (encounterId: string, respostas: Record<string, string>) =>
    http.post(`/auth/paciente/atendimentos/${encounterId}/pesquisa`, { respostas }),
  translados: () =>
    comCacheLocal('translados', () =>
      http.get<Translado[]>('/auth/paciente/meus-translados').then((r) => r.data)),
  atendimentos: () =>
    comCacheLocal('atendimentos', () =>
      http.get<Atendimento[]>('/auth/paciente/atendimentos').then((r) => r.data)),
  exames: () =>
    comCacheLocal('exames', () => http.get<Exame[]>('/auth/paciente/exames').then((r) => r.data)),
  laudos: () =>
    comCacheLocal('laudos', () => http.get<Laudo[]>('/auth/paciente/laudos').then((r) => r.data)),
  agendamentos: (tipo: 'consulta' | 'exame') =>
    comCacheLocal(`agendamentos.${tipo}`, () =>
      http.get<Agendamento[]>('/auth/paciente/agendamentos', { params: { tipo } }).then((r) => r.data)),
  agendamentoExame: (solicitacaoExameId: string) =>
    comCacheLocal(`agendamento.${solicitacaoExameId}`, () =>
      http
        .get<AgendamentoExameDetalhe>(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}`)
        .then((r) => r.data)),
  confirmarExame: (solicitacaoExameId: string) =>
    http.post(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}/confirmar`),
  cancelarExame: (solicitacaoExameId: string, motivo: string) =>
    http.post(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}/cancelar`, { motivo }),
};

// URLs de PDF protegido (abertas/baixadas via blob — ver lib/pdf.ts).
export const pdfUrls = {
  laudo: (laudoId: string) => `/auth/paciente/laudos/${laudoId}/pdf`,
  anexo: (anexoId: string) => `/auth/paciente/anexos/${anexoId}/conteudo`,
  exameImagens: (solicitacaoId: string) => `/auth/paciente/exames/${solicitacaoId}/imagens-pdf`,
};
