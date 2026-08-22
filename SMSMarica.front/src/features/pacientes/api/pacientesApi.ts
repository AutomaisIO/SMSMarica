import { http } from '@/shared/api/httpClient';
import type { AnexoExameDto } from '@/features/anamnese/types';
import type { PaginaAuditoria } from '@/features/auditoria/types';
import type {
  AgendamentosPaciente,
  Atendimento,
  AtualizarPacientePayload,
  CadastrarPacientePayload,
  Paciente,
  PacienteExistencia,
  PacienteListItem,
} from '@/features/pacientes/types';

export async function obterAtendimentos(id: string): Promise<Atendimento[]> {
  const { data } = await http.get<Atendimento[]>(`/pacientes/${id}/atendimentos`);
  return data;
}

/** Agendamentos do paciente (SER + SISREG + agenda local), separados em próximos e histórico. */
export async function obterAgendamentosPaciente(id: string): Promise<AgendamentosPaciente> {
  const { data } = await http.get<AgendamentosPaciente>(`/pacientes/${id}/agendamentos`);
  return data;
}

/** Histórico de exames digitalizados (DocumentoExame status=Salvo) do paciente. */
export async function obterAnexosExame(id: string): Promise<AnexoExameDto[]> {
  const { data } = await http.get<AnexoExameDto[]>(`/pacientes/${id}/anexos-exame`);
  return data;
}

export type AcessoCidadao = {
  id: string;
  canal: string;
  dispositivo: string | null;
  ip: string | null;
  criadaEm: string;
  expiraEm: string;
  revogadaEm: string | null;
  ativa: boolean;
};

export async function obterAcessos(id: string): Promise<AcessoCidadao[]> {
  const { data } = await http.get<AcessoCidadao[]>(`/pacientes/${id}/acessos`);
  return data;
}

export async function buscarPacientes(termo: string): Promise<PacienteListItem[]> {
  const t = termo.trim();
  // Sem termo o backend devolve os 10 últimos cadastros; com termo, busca por
  // nome (qualquer parte) ou CPF.
  const { data } = await http.get<PacienteListItem[]>('/pacientes', {
    params: t ? { termo: t } : undefined,
  });
  return data;
}

export async function obterPacientePorId(id: string): Promise<Paciente> {
  const { data } = await http.get<Paciente>(`/pacientes/${id}`);
  return data;
}

export async function obterPacientePorCpf(cpf: string): Promise<PacienteExistencia | null> {
  const limpo = cpf.replace(/\D/g, '');
  if (limpo.length !== 11) return null;
  const resposta = await http.get<PacienteExistencia>(`/pacientes/por-cpf/${limpo}`, {
    validateStatus: (s) => s === 200 || s === 404,
  });
  return resposta.status === 404 ? null : resposta.data;
}

export async function cadastrarPaciente(payload: CadastrarPacientePayload): Promise<string> {
  const { data } = await http.post<string>('/pacientes', payload);
  return data;
}

export async function atualizarPaciente(
  id: string,
  payload: AtualizarPacientePayload,
): Promise<void> {
  await http.put(`/pacientes/${id}`, payload);
}

/**
 * Corrige o nome oficial do paciente (fluxo "Verificar nome"). Endpoint dedicado
 * porque o PUT normal trata o nome como imutável. A mudança é auditada no backend.
 */
export async function atualizarNomePaciente(id: string, nomeCompleto: string): Promise<void> {
  await http.put(`/pacientes/${id}/nome`, { nomeCompleto });
}

/** Histórico de alterações auditadas deste paciente (ex.: correções de nome). */
export async function obterAuditoriaPaciente(id: string): Promise<PaginaAuditoria> {
  const { data } = await http.get<PaginaAuditoria>(`/pacientes/${id}/auditoria`);
  return data;
}

export async function desativarPaciente(id: string): Promise<void> {
  await http.delete(`/pacientes/${id}`);
}

export async function reativarPaciente(id: string): Promise<void> {
  await http.post(`/pacientes/${id}/reativar`);
}

/**
 * Dispara a pesquisa de satisfação de um atendimento ao paciente (WhatsApp). Idempotente por
 * atendimento no servidor: reenviar não gera segunda pesquisa nem segunda nota.
 */
export async function enviarPesquisaSatisfacao(
  pacienteId: string,
  encounterId: string,
): Promise<{ pesquisaId: string; url: string; jaEnviadaAntes: boolean }> {
  const { data } = await http.post('/pesquisas-satisfacao/enviar', { pacienteId, encounterId });
  return data;
}
