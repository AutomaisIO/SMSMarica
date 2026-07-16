import { http } from '@/shared/api/httpClient';
import type { ErroFiltro, PaginaErros, RegistroErro } from '@/features/erros/types';

export async function buscarErros(filtro: ErroFiltro): Promise<PaginaErros> {
  const params: Record<string, string | number> = {};
  for (const [k, v] of Object.entries(filtro)) {
    if (v !== undefined && v !== null && v !== '') params[k] = v as string | number;
  }
  const { data } = await http.get<PaginaErros>('/erros', { params });
  return data;
}

export async function obterErroPorCodigo(codigo: string): Promise<RegistroErro> {
  const { data } = await http.get<RegistroErro>(`/erros/${encodeURIComponent(codigo)}`);
  return data;
}

export async function resolverErro(
  codigo: string,
  body: { resolvidoPor?: string; nota?: string },
): Promise<RegistroErro> {
  const { data } = await http.post<RegistroErro>(`/erros/${encodeURIComponent(codigo)}/resolver`, body);
  return data;
}

export async function reabrirErro(codigo: string): Promise<RegistroErro> {
  const { data } = await http.post<RegistroErro>(`/erros/${encodeURIComponent(codigo)}/reabrir`);
  return data;
}
