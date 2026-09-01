import { http } from '@/shared/api/httpClient';
import type { PendenciaCadastro, StatusPendenciaCadastro } from '@/features/pendencias-cadastro/types';

export async function listarPendencias(status?: StatusPendenciaCadastro): Promise<PendenciaCadastro[]> {
  const { data } = await http.get<PendenciaCadastro[]>('/pendencias-cadastro', {
    params: { status: status ?? undefined },
  });
  return data;
}

export async function resolverPendencia(id: string, nota: string | null): Promise<void> {
  await http.post(`/pendencias-cadastro/${id}/resolver`, { nota });
}

export async function ignorarPendencia(id: string, nota: string | null): Promise<void> {
  await http.post(`/pendencias-cadastro/${id}/ignorar`, { nota });
}

export type VarreduraResultado = {
  horas: number;
  mensagensLidas: number;
  conversasCandidatas: number;
  erros: number;
  pendenciasRegistradas: number;
  jaExistiam: number;
  descartadasPeloModelo: number;
  itens: { conversaId: string; telefoneCanonical: string; pacienteId: string | null; acao: string; resumo: string | null }[];
};

/** Varre as conversas recentes atrás de "não sou essa pessoa" (padrões + IA) e registra pendências. */
export async function varrerContatosNegados(horas: number): Promise<VarreduraResultado> {
  const { data } = await http.post<VarreduraResultado>('/pendencias-cadastro/varredura', { horas });
  return data;
}
