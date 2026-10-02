import { http } from '@/shared/api/httpClient';

import type { SistemaRegulacao } from '../types';

/** Espelha `SituacaoMedicoPendente` (viaja como string). */
export type SituacaoMedicoPendente = 'Pendente' | 'Cadastrado' | 'JaExistia' | 'Recusado';

/** O que parece o mesmo médico — na lista do sistema ou entre os já pedidos. */
export type MedicoParecido = {
  nome: string;
  /** "Sistema" = já está na lista do SER/SERNIT; "Pendente" = alguém já pediu. */
  origem: 'Sistema' | 'Pendente';
  /** O valor a pôr no campo de médico da solicitação. */
  valor: string;
  pontuacao: number;
  motivo: string;
  pendenteId: string | null;
};

export type MedicoPendente = {
  id: string;
  sistema: SistemaRegulacao;
  nome: string;
  tipoDocumento: string | null;
  numeroDocumento: string | null;
  especialidade: string | null;
  situacao: SituacaoMedicoPendente;
  nomeNoSistema: string | null;
  motivo: string | null;
  criadoEm: string;
  resolvidoEm: string | null;
  /** `pendente:{id}` — o valor que fica no campo de médico até a regulação resolver. */
  valor: string;
};

export const TIPOS_DOCUMENTO = ['CRM', 'CNS', 'RG', 'CPF', 'PMM', 'RMS'] as const;

/** O campo de médico da solicitação aponta para um pedido de cadastro ainda não resolvido. */
export function idPendente(valor: string | null | undefined): string | null {
  return valor?.startsWith('pendente:') ? valor.slice('pendente:'.length) : null;
}

export async function buscarMedicosParecidos(
  sistema: SistemaRegulacao,
  nome: string,
  documento: string | null,
): Promise<MedicoParecido[]> {
  const { data } = await http.get<MedicoParecido[]>('/regulacao/medicos/parecidos', {
    params: { sistema, nome, documento: documento || undefined },
  });
  return data;
}

export async function pedirMedico(dados: {
  sistema: SistemaRegulacao;
  nome: string;
  tipoDocumento: string | null;
  numeroDocumento: string | null;
  especialidade: string | null;
}): Promise<MedicoPendente> {
  const { data } = await http.post<MedicoPendente>('/regulacao/medicos/pendentes', dados);
  return data;
}

export async function obterMedicoPendente(id: string): Promise<MedicoPendente> {
  const { data } = await http.get<MedicoPendente>(`/regulacao/medicos/pendentes/${id}`);
  return data;
}

export async function resolverMedicoPendente(
  id: string,
  acao: Exclude<SituacaoMedicoPendente, 'Pendente'>,
  nomeNoSistema: string | null,
  motivo: string | null,
): Promise<MedicoPendente> {
  const { data } = await http.post<MedicoPendente>(`/regulacao/medicos/pendentes/${id}/resolver`, {
    acao,
    nomeNoSistema,
    motivo,
  });
  return data;
}

/** Os pedidos de cadastro de um sistema — a fila do técnico da regulação. */
export async function listarMedicosPendentes(
  sistema: SistemaRegulacao,
  situacao: SituacaoMedicoPendente | null,
): Promise<MedicoPendente[]> {
  const { data } = await http.get<MedicoPendente[]>('/regulacao/medicos/pendentes', {
    params: { sistema, situacao: situacao ?? undefined },
  });
  return data;
}

/** Os médicos na lista do próprio sistema (a cópia do combo "Médico responsável"). */
export async function listarMedicosDoSistema(sistema: SistemaRegulacao): Promise<string[]> {
  const { data } = await http.get<string[]>('/regulacao/medicos/lista', { params: { sistema } });
  return data;
}
