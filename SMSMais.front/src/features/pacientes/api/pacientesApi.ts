import { http } from '@/shared/api/httpClient';
import type { AnexoExameDto } from '@/features/anamnese/types';
import type { PaginaAuditoria } from '@/features/auditoria/types';
import type { Mensagem } from '@/features/conversas/types';
import type {
  AgendamentosPaciente,
  Atendimento,
  AtualizarPacientePayload,
  CadastrarPacientePayload,
  Paciente,
  PacienteExistencia,
  PacienteListItem,
  PreviaUnificacao,
  ResultadoUnificacao,
  SessaoConversaPaciente,
  UnificarPacientesPayload,
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

export type PlataformaAparelho = 'android' | 'ios';

/** Sessão do app ativa e com o token de notificação registrado — é ela que recebe o envio. */
export type AparelhoAppCidadao = {
  sessaoId: string;
  plataforma: PlataformaAparelho;
  registradoEm: string;
  entrouEm: string;
  dispositivo: string | null;
};

export type NotificacaoAppEnviada = {
  id: string;
  titulo: string;
  mensagem: string;
  rota: string | null;
  criadaEm: string;
  enviadaPor: string | null;
  aparelhos: number;
  entregues: number;
  falha: string | null;
};

export type AppCidadaoPaciente = {
  /** Credencial do Firebase gravada e ativa em Integrações. */
  configurado: boolean;
  aparelhos: AparelhoAppCidadao[];
  /** As 20 mais recentes, recentes primeiro. */
  notificacoes: NotificacaoAppEnviada[];
};

export type EnviarNotificacaoAppPayload = {
  titulo: string;
  mensagem: string;
  /** Tela do app que o toque abre; null = Início. */
  rota: string | null;
};

export type ResultadoAparelhoNotificacao = {
  plataforma: PlataformaAparelho;
  entregue: boolean;
  detalhe: string | null;
  /** O Firebase disse que o token morreu: o aparelho saiu da lista e não recebe mais. */
  aparelhoRemovido: boolean;
};

export type ResultadoEnvioNotificacaoApp = {
  notificacaoId: string;
  aparelhos: number;
  entregues: number;
  resultados: ResultadoAparelhoNotificacao[];
};

export async function obterAppCidadao(id: string): Promise<AppCidadaoPaciente> {
  const { data } = await http.get<AppCidadaoPaciente>(`/pacientes/${id}/app-cidadao`);
  return data;
}

/**
 * Envia a notificação a todos os aparelhos ativos do paciente. Com aparelho, o servidor sempre
 * grava o histórico e devolve 200 com o desfecho de cada um — mesmo que nenhum tenha aceitado.
 * Sem configuração (400) ou sem aparelho (409), nada é gravado.
 */
export async function enviarNotificacaoApp(
  id: string,
  corpo: EnviarNotificacaoAppPayload,
): Promise<ResultadoEnvioNotificacaoApp> {
  const { data } = await http.post<ResultadoEnvioNotificacaoApp>(
    `/pacientes/${id}/app-cidadao/notificacoes`,
    corpo,
  );
  return data;
}

/** Sessões de conversa de WhatsApp do paciente (blocos por 24h+ de silêncio), recentes primeiro. */
export async function obterSessoesConversa(id: string): Promise<SessaoConversaPaciente[]> {
  const { data } = await http.get<SessaoConversaPaciente[]>(`/pacientes/${id}/conversas/sessoes`);
  return data;
}

/** Mensagens de uma sessão (telefone + faixa vindos da listagem), em ordem cronológica. */
export async function obterMensagensSessao(
  id: string,
  telefone: string,
  de: string,
  ate: string,
): Promise<Mensagem[]> {
  const { data } = await http.get<Mensagem[]>(`/pacientes/${id}/conversas/mensagens`, {
    params: { telefone, de, ate },
  });
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

/**
 * Prévia (dry-run) da unificação: os dois cadastros completos, os campos que divergem e
 * quantas referências de cada módulo serão movidas. Não altera nada.
 */
export async function preverUnificacao(
  sobrevivente: string,
  absorvido: string,
): Promise<PreviaUnificacao> {
  const { data } = await http.get<PreviaUnificacao>('/pacientes/unificar/previa', {
    params: { sobrevivente, absorvido },
  });
  return data;
}

/** Unifica dois cadastros do mesmo paciente (ver UnificarPacientesPayload). */
export async function unificarPacientes(
  payload: UnificarPacientesPayload,
): Promise<ResultadoUnificacao> {
  const { data } = await http.post<ResultadoUnificacao>('/pacientes/unificar', payload);
  return data;
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
