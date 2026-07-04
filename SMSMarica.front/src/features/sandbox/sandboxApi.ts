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
};
