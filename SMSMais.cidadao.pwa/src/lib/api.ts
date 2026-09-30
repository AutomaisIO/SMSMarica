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

/** Uma viagem do Transporte de Pacientes (sessão do atendimento). */
export type ViagemTransporte = {
  sessaoId: string;
  data: string;
  destino: string;
  cidade: string | null;
  tipoTratamento: string | null;
  /** Pendente | Confirmada | Realizada | Cancelada | NaoRealizada | AguardandoRetorno */
  status: string;
  /** "HH:mm:ss" quando a rota já definiu; senão, informado na véspera. */
  horaBusca: string | null;
  acompanhantes: string[];
  limiteAcompanhantes: number;
};

export const PARENTESCOS: { valor: string; rotulo: string }[] = [
  { valor: 'Mae', rotulo: 'Mãe' },
  { valor: 'Pai', rotulo: 'Pai' },
  { valor: 'Filho', rotulo: 'Filho(a)' },
  { valor: 'Conjuge', rotulo: 'Cônjuge' },
  { valor: 'Irmao', rotulo: 'Irmão(ã)' },
  { valor: 'OutroParente', rotulo: 'Outro parente' },
  { valor: 'Cuidador', rotulo: 'Cuidador(a)' },
  { valor: 'Outro', rotulo: 'Outro' },
];

/** Pessoa que pode acompanhar o paciente no transporte. */
export type AcompanhanteCidadao = {
  id: string;
  cpf: string;
  nome: string;
  dataNascimento: string;
  parentesco: string | null;
  telefone: string | null;
  /** Painel = cadastrado pela equipe; App = pelo próprio paciente. */
  origem: 'Painel' | 'App';
};

export type ConsultaAcompanhante = { cpf: string; nome: string; dataNascimento: string; jaCadastrado: boolean };
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
  // Nulo quando o pedido ainda está NA FILA da regulação (sem data).
  inicioEm: string | null;
  fimEm: string | null;
  tipo: 'Consulta' | 'Exame';
  titulo: string;
  profissional: string | null;
  unidade: string | null;
  status: string;
  // Exame importado (SISREG): habilita confirmar/avisar ausência no card.
  solicitacaoExameId: string | null;
  statusConfirmacao: 'Pendente' | 'Confirmada' | 'Cancelada' | null;
  podeResponder: boolean;
  // Pedido da regulação externa (SER, SERNIT, ESUS SG): quem marcou, em linguagem do paciente.
  origem?: string | null;
  // Na fila da regulação: só "está na fila" — nunca motivo de pendência, posição ou previsão.
  naFila?: boolean;
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
  /** Decidido no back: true só no dia do atendimento (Brasília). */
  chaveAcessoDisponivelHoje: boolean;
};

/** Chave de acesso (confirmação do SISREG) — entregue só no dia do exame. */
export type ChaveAcessoExame = { chave: string; codigoSolicitacao: string };

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
      http.get<ViagemTransporte[]>('/auth/paciente/meus-translados').then((r) => r.data)),
  acompanhantes: () =>
    comCacheLocal('acompanhantes', () =>
      http.get<AcompanhanteCidadao[]>('/auth/paciente/me/acompanhantes').then((r) => r.data)),
  /** Confere CPF + nascimento e traz o nome para confirmar. Tem cota diária. */
  consultarAcompanhante: (cpf: string, dataNascimento: string) =>
    http
      .post<ConsultaAcompanhante>('/auth/paciente/me/acompanhantes/consulta', { cpf, dataNascimento })
      .then((r) => r.data),
  adicionarAcompanhante: (cpf: string, dataNascimento: string, parentesco: string | null) =>
    http
      .post<AcompanhanteCidadao>('/auth/paciente/me/acompanhantes', { cpf, dataNascimento, parentesco, telefone: null })
      .then((r) => r.data),
  removerAcompanhante: (id: string) => http.delete(`/auth/paciente/me/acompanhantes/${id}`),
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
  // Sem cache local de propósito: a chave é lida na hora (o back decide se é o dia).
  chaveAcessoExame: (solicitacaoExameId: string) =>
    http
      .post<ChaveAcessoExame>(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}/chave-acesso`)
      .then((r) => r.data),
  cancelarExame: (solicitacaoExameId: string, motivo: string) =>
    http.post(`/auth/paciente/agendamentos/exames/${solicitacaoExameId}/cancelar`, { motivo }),
};

// URLs de PDF protegido (abertas/baixadas via blob — ver lib/pdf.ts).
export const pdfUrls = {
  laudo: (laudoId: string) => `/auth/paciente/laudos/${laudoId}/pdf`,
  anexo: (anexoId: string) => `/auth/paciente/anexos/${anexoId}/conteudo`,
  exameImagens: (solicitacaoId: string) => `/auth/paciente/exames/${solicitacaoId}/imagens-pdf`,
};
