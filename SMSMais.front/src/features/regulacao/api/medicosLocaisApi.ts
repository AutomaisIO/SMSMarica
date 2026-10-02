import { http } from '@/shared/api/httpClient';

import type { SistemaRegulacao } from '../types';

/** Espelha `OrigemMedicoLocal` (viaja como string). */
export type OrigemMedicoLocal = 'Fichas' | 'Executantes' | 'Plataforma';

/** Médico do cadastro que é só nosso (hoje, o do SISREG — lá o médico é texto digitado). */
export type MedicoLocal = {
  id: string;
  sistema: SistemaRegulacao;
  nome: string;
  cpf: string | null;
  conselho: string | null;
  numeroConselho: string | null;
  ufConselho: string | null;
  /** As outras formas do nome vistas nas fichas (sem acento) — a busca também as procura. */
  grafias: string[];
  origem: OrigemMedicoLocal;
  /** Pedidos com este médico nas fichas importadas. */
  ocorrencias: number;
};

export type MedicoLocalParecido = { medico: MedicoLocal; pontuacao: number; motivo: string };

export type PossivelRepetido = { a: MedicoLocal; b: MedicoLocal; pontuacao: number };

export type DadosMedicoLocal = {
  sistema: SistemaRegulacao;
  nome: string;
  cpf: string | null;
  conselho: string | null;
  numeroConselho: string | null;
  ufConselho: string | null;
};

export async function buscarMedicosLocais(
  sistema: SistemaRegulacao,
  termo: string,
  limite = 20,
): Promise<MedicoLocal[]> {
  const { data } = await http.get<MedicoLocal[]>('/regulacao/medicos/locais', {
    params: { sistema, termo: termo || undefined, limite },
  });
  return data;
}

export async function parecidosMedicoLocal(
  sistema: SistemaRegulacao,
  nome: string,
  cpf: string | null,
): Promise<MedicoLocalParecido[]> {
  const { data } = await http.get<MedicoLocalParecido[]>('/regulacao/medicos/locais/parecidos', {
    params: { sistema, nome, cpf: cpf || undefined },
  });
  return data;
}

/** Inclui — ou devolve o que já existe com o mesmo CPF ou nome. */
export async function criarMedicoLocal(dados: DadosMedicoLocal): Promise<MedicoLocal> {
  const { data } = await http.post<MedicoLocal>('/regulacao/medicos/locais', dados);
  return data;
}

export async function atualizarMedicoLocal(id: string, dados: DadosMedicoLocal): Promise<MedicoLocal> {
  const { data } = await http.put<MedicoLocal>(`/regulacao/medicos/locais/${id}`, dados);
  return data;
}

/** Junta `origemId` em `destinoId` (o primeiro deixa de existir). */
export async function juntarMedicosLocais(destinoId: string, origemId: string): Promise<MedicoLocal> {
  const { data } = await http.post<MedicoLocal>(`/regulacao/medicos/locais/${destinoId}/juntar/${origemId}`);
  return data;
}

export async function possiveisRepetidos(sistema: SistemaRegulacao): Promise<PossivelRepetido[]> {
  const { data } = await http.get<PossivelRepetido[]>('/regulacao/medicos/locais/possiveis-repetidos', {
    params: { sistema },
  });
  return data;
}

export async function atualizarMedicosDasFichas(
  sistema: SistemaRegulacao,
): Promise<{ lidos: number; criados: number; atualizados: number; total: number }> {
  const { data } = await http.post('/regulacao/medicos/locais/atualizar-das-fichas', null, { params: { sistema } });
  return data;
}

/** "123.456.789-01" — só para exibir. */
export function formatarCpf(cpf: string | null): string {
  return cpf && cpf.length === 11 ? `${cpf.slice(0, 3)}.${cpf.slice(3, 6)}.${cpf.slice(6, 9)}-${cpf.slice(9)}` : '';
}
