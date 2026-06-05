import { http } from '@/shared/api/httpClient';
import type {
  Especialidade,
  EspecialidadeListItem,
  SalvarEspecialidadePayload,
} from '@/features/especialidades/types';

export async function listarEspecialidades(incluirInativas = false): Promise<EspecialidadeListItem[]> {
  const { data } = await http.get<EspecialidadeListItem[]>('/especialidades', {
    params: { incluirInativas: incluirInativas ? 'true' : undefined },
  });
  return data;
}

export async function obterEspecialidade(id: string): Promise<Especialidade> {
  const { data } = await http.get<Especialidade>(`/especialidades/${id}`);
  return data;
}

export async function cadastrarEspecialidade(payload: SalvarEspecialidadePayload): Promise<string> {
  const { data } = await http.post<string>('/especialidades', {
    nome: payload.nome,
    codigoCbo: payload.codigoCbo || null,
  });
  return data;
}

export async function atualizarEspecialidade(
  id: string,
  payload: SalvarEspecialidadePayload,
): Promise<void> {
  await http.put(`/especialidades/${id}`, {
    nome: payload.nome,
    codigoCbo: payload.codigoCbo || null,
    ativo: payload.ativo ?? true,
  });
}

export async function excluirEspecialidade(id: string): Promise<void> {
  await http.delete(`/especialidades/${id}`);
}
