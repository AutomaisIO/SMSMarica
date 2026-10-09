import { http } from '@/shared/api/httpClient';

export type SandboxPaciente = { id: string; nome: string; cpf: string | null };
export type SandboxSolicitacao = {
  id: string;
  accessionNumber: string;
  tipoExame: string | null;
  dataAgendada: string | null;
  status: string;
  statusConfirmacao: string;
};
export type LinkTeste = { url: string; expiraEm: string };
/** "Entrar como paciente": o CPF do operador no app abre como o paciente escolhido. */
export type PersonificacaoStatus = {
  apta: boolean;
  motivoInapta: string | null;
  cpfMascarado: string | null;
  telefoneMascarado: string | null;
  ativa: {
    pacienteId: string;
    pacienteNome: string;
    criadaEm: string;
    expiraEm: string;
    sessoesAbertas: number;
  } | null;
};
export type ResultadoEnvio = { ok: boolean; erro: string | null; link: string | null };

export const sandboxApi = {
  buscarPacientes: (termo: string) =>
    http.get<SandboxPaciente[]>('/sandbox/pacientes', { params: { termo } }).then((r) => r.data),
  solicitacoes: (pacienteId: string) =>
    http.get<SandboxSolicitacao[]>(`/sandbox/pacientes/${pacienteId}/solicitacoes`).then((r) => r.data),
  gerarLink: (pacienteId: string, destino?: string) =>
    http.post<LinkTeste>('/sandbox/magic-link', { pacienteId, destino }).then((r) => r.data),
  enviar: (body: { telefone: string; texto: string; pacienteId?: string; destino?: string }) =>
    http.post<ResultadoEnvio>('/sandbox/mensagem', body).then((r) => r.data),
  definirConfirmacao: (solicitacaoExameId: string, estado: string, motivo?: string) =>
    http.post('/sandbox/confirmacao', { solicitacaoExameId, estado, motivo }),
  /** Simula o ciclo dos checks do zap (sem Meta): enviada|entregue|lida|visualizada|falha|reset. */
  simularComunicacao: (solicitacaoExameId: string, finalidade: string, estado: string) =>
    http.post('/sandbox/comunicacao', { solicitacaoExameId, finalidade, estado }),
  personificacao: () => http.get<PersonificacaoStatus>('/sandbox/personificacao').then((r) => r.data),
  ativarPersonificacao: (pacienteId: string) =>
    http.post<PersonificacaoStatus>('/sandbox/personificacao', { pacienteId }).then((r) => r.data),
  encerrarPersonificacao: () => http.delete('/sandbox/personificacao'),
};
