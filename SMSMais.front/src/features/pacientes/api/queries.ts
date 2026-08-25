import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  atualizarNomePaciente,
  atualizarPaciente,
  buscarPacientes,
  cadastrarPaciente,
  desativarPaciente,
  obterAcessos,
  obterAgendamentosPaciente,
  obterAnexosExame,
  obterAtendimentos,
  obterAuditoriaPaciente,
  obterMensagensSessao,
  obterPacientePorCpf,
  obterPacientePorId,
  obterSessoesConversa,
  reativarPaciente,
} from '@/features/pacientes/api/pacientesApi';
import type {
  AtualizarPacientePayload,
  CadastrarPacientePayload,
} from '@/features/pacientes/types';

export const pacientesKeys = {
  raiz: ['pacientes'] as const,
  busca: (termo: string) => ['pacientes', 'busca', termo] as const,
  porId: (id: string) => ['pacientes', 'detalhe', id] as const,
  porCpf: (cpf: string) => ['pacientes', 'por-cpf', cpf] as const,
  atendimentos: (id: string) => ['pacientes', 'atendimentos', id] as const,
  agendamentos: (id: string) => ['pacientes', 'agendamentos', id] as const,
  acessos: (id: string) => ['pacientes', 'acessos', id] as const,
  anexosExame: (id: string) => ['pacientes', 'anexos-exame', id] as const,
  auditoria: (id: string) => ['pacientes', 'auditoria', id] as const,
  sessoesConversa: (id: string) => ['pacientes', 'sessoes-conversa', id] as const,
  mensagensSessao: (id: string, telefone: string, de: string) =>
    ['pacientes', 'mensagens-sessao', id, telefone, de] as const,
};

export function useAtendimentosPaciente(id: string | null) {
  return useQuery({
    queryKey: id ? pacientesKeys.atendimentos(id) : ['pacientes', 'atendimentos', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterAtendimentos(id);
    },
    enabled: Boolean(id),
  });
}

export function useAgendamentosPaciente(id: string | null) {
  return useQuery({
    queryKey: id ? pacientesKeys.agendamentos(id) : ['pacientes', 'agendamentos', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterAgendamentosPaciente(id);
    },
    enabled: Boolean(id),
  });
}

export function useAcessosPaciente(id: string | null) {
  return useQuery({
    queryKey: id ? pacientesKeys.acessos(id) : ['pacientes', 'acessos', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterAcessos(id);
    },
    enabled: Boolean(id),
  });
}

export function useAnexosExamePaciente(id: string | null) {
  return useQuery({
    queryKey: id ? pacientesKeys.anexosExame(id) : ['pacientes', 'anexos-exame', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterAnexosExame(id);
    },
    enabled: Boolean(id),
  });
}

export function useSessoesConversaPaciente(id: string | null) {
  return useQuery({
    queryKey: id ? pacientesKeys.sessoesConversa(id) : ['pacientes', 'sessoes-conversa', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterSessoesConversa(id);
    },
    enabled: Boolean(id),
  });
}

/** Mensagens de UMA sessão — só busca quando a sessão é expandida na aba. */
export function useMensagensSessaoPaciente(
  id: string,
  sessao: { telefone: string; inicio: string; fim: string } | null,
) {
  return useQuery({
    queryKey: sessao
      ? pacientesKeys.mensagensSessao(id, sessao.telefone, sessao.inicio)
      : ['pacientes', 'mensagens-sessao', 'nenhuma'],
    queryFn: () => {
      if (!sessao) throw new Error('Sessão não informada.');
      return obterMensagensSessao(id, sessao.telefone, sessao.inicio, sessao.fim);
    },
    enabled: Boolean(sessao),
    staleTime: 60_000,
  });
}

export function useBuscarPacientes(termo: string) {
  const t = termo.trim();
  return useQuery({
    queryKey: pacientesKeys.busca(termo),
    queryFn: () => buscarPacientes(termo),
    // Vazio: backend devolve os 10 últimos cadastros. Com 1 char a busca seria
    // ampla demais — espera o segundo caractere. >= 2: busca normal.
    enabled: t.length === 0 || t.length >= 2,
    placeholderData: (anterior) => anterior,
  });
}

export function usePacientePorId(id: string | null) {
  return useQuery({
    queryKey: id ? pacientesKeys.porId(id) : ['pacientes', 'detalhe', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterPacientePorId(id);
    },
    enabled: Boolean(id),
  });
}

export function useCadastrarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (payload: CadastrarPacientePayload) => cadastrarPaciente(payload),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
    },
  });
}

export function useAtualizarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, payload }: { id: string; payload: AtualizarPacientePayload }) =>
      atualizarPaciente(id, payload),
    onSuccess: (_data, variables) => {
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
      client.invalidateQueries({ queryKey: pacientesKeys.porId(variables.id) });
    },
  });
}

export function useAuditoriaPaciente(id: string | null) {
  return useQuery({
    queryKey: id ? pacientesKeys.auditoria(id) : ['pacientes', 'auditoria', 'nenhum'],
    queryFn: () => {
      if (!id) throw new Error('ID não informado.');
      return obterAuditoriaPaciente(id);
    },
    enabled: Boolean(id),
  });
}

/** Corrige o nome oficial do paciente (fluxo "Verificar nome"). */
export function useAtualizarNomePaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({ id, nomeCompleto }: { id: string; nomeCompleto: string }) =>
      atualizarNomePaciente(id, nomeCompleto),
    onSuccess: (_data, variables) => {
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
      client.invalidateQueries({ queryKey: pacientesKeys.porId(variables.id) });
      client.invalidateQueries({ queryKey: pacientesKeys.auditoria(variables.id) });
    },
  });
}

export function useDesativarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => desativarPaciente(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
    },
  });
}

export function useReativarPaciente() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => reativarPaciente(id),
    onSuccess: () => {
      client.invalidateQueries({ queryKey: pacientesKeys.raiz });
    },
  });
}

export async function consultarPacientePorCpf(cpf: string) {
  return obterPacientePorCpf(cpf);
}
