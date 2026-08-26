import { http } from '@/shared/api/httpClient';
import type {
  ComandoRoboCatalogo,
  RoboAssunto,
  RoboAssuntoListItem,
  RoboConfiguracao,
  SalvarRoboAssuntoPayload,
} from '@/features/robo-atendimento/types';

export async function listarAssuntos(incluirInativos = false): Promise<RoboAssuntoListItem[]> {
  const { data } = await http.get<RoboAssuntoListItem[]>('/robo/assuntos', {
    params: { incluirInativos: incluirInativos ? 'true' : undefined },
  });
  return data;
}

export async function obterAssunto(id: string): Promise<RoboAssunto> {
  const { data } = await http.get<RoboAssunto>(`/robo/assuntos/${id}`);
  return data;
}

export async function catalogoComandos(): Promise<ComandoRoboCatalogo[]> {
  const { data } = await http.get<ComandoRoboCatalogo[]>('/robo/assuntos/catalogo-comandos');
  return data;
}

export async function criarAssunto(payload: SalvarRoboAssuntoPayload): Promise<string> {
  const { data } = await http.post<string>('/robo/assuntos', payload);
  return data;
}

export async function atualizarAssunto(id: string, payload: SalvarRoboAssuntoPayload): Promise<void> {
  await http.put(`/robo/assuntos/${id}`, payload);
}

export async function excluirAssunto(id: string): Promise<void> {
  await http.delete(`/robo/assuntos/${id}`);
}

export async function obterConfiguracao(): Promise<RoboConfiguracao> {
  const { data } = await http.get<RoboConfiguracao>('/robo/configuracao');
  return data;
}

export async function salvarConfiguracao(payload: RoboConfiguracao): Promise<void> {
  await http.put('/robo/configuracao', payload);
}
